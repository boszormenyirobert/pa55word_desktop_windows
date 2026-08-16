using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    public class BootstrapRegistrationService
    {
        private readonly RegistrationConfigStore _configStore = new();
        private CancellationTokenSource _registrationCts = new();
        private PendingRegistrationContext? _pendingServerRegistration;

        public event Action? RegistrationCompleted;
        public event Action<string>? RegistrationFailed;

        public Task<bool> LoadOrCreateConfig()
        {
            if (_configStore.TryReadRegistration(out _))
            {
                return Task.FromResult(true);
            }

            ConfigModel bootstrapConfig = _configStore.LoadBootstrapConfig();

            if (!_configStore.FileExists)
            {
                _configStore.SaveBootstrapConfig(bootstrapConfig);
            }

            return Task.FromResult(false);
        }

        public void StartRegistrationPolling(string? baseUrlOverride = null)
        {
            if (_registrationCts.IsCancellationRequested)
            {
                _registrationCts.Dispose();
                _registrationCts = new CancellationTokenSource();
            }

            CancellationTokenSource registrationCts = _registrationCts;
            _ = Task.Run(() => PollRegistrationApi(baseUrlOverride, registrationCts));
        }

        public string GetBootstrapBaseUrl()
        {
            return _configStore.LoadBootstrapConfig().BaseUrl;
        }

        public async Task PrepareRegistrationQrAsync(string baseUrl)
        {
            _pendingServerRegistration = null;

            ConfigModel bootstrapConfig = _configStore.LoadBootstrapConfig();
            bootstrapConfig.BaseUrl = NormalizeBaseUrl(baseUrl);
            await PrepareBootstrapConfigAndQrAsync(bootstrapConfig).ConfigureAwait(false);
        }

        public async Task PrepareServerRegistrationQrAsync(string namespaceUrl, string namespaceName)
        {
            var pki = new Pki();
            string deviceId = GenerateId();
            string keyContainer = "KeyContainer_" + deviceId;
            string normalizedNamespaceUrl = NormalizeBaseUrl(namespaceUrl);
            string normalizedNamespaceName = NormalizeNamespaceName(namespaceName);
            string namespaceId = CreateNamespaceId(normalizedNamespaceName, normalizedNamespaceUrl);

            Debug.WriteLine(
                $"[NS-ADD] Preparing namespace QR. deviceId='{deviceId}', namespaceId='{namespaceId}', namespaceUrl='{normalizedNamespaceUrl}', namespaceName='{normalizedNamespaceName}'.");

            pki.CreatePrivateKey(keyContainer);
            string publicKey = pki.GetPublicKeyDerBase64(keyContainer);

            Debug.WriteLine($"[NS-ADD] Namespace keypair ready. keyContainer='{keyContainer}', publicKeyLength={publicKey.Length}.");

            _pendingServerRegistration = new PendingRegistrationContext(
                deviceId,
                namespaceId,
                normalizedNamespaceUrl);

            _configStore.UpsertNamespace(new NamespaceItemDto
            {
                DeviceId = deviceId,
                DeviceName = Environment.MachineName,
                NamespaceId = namespaceId,
                Namespace = normalizedNamespaceName,
                Url = normalizedNamespaceUrl
            });

            string payload = JsonSerializer.Serialize(new
            {
                type = "namespace",
                deviceId,
                publicKey,
                deviceName = Environment.MachineName,
                namespaceId,
                namespaceUrl = normalizedNamespaceUrl,
                namespaceName = normalizedNamespaceName
            });

            Debug.WriteLine($"[NS-ADD] Namespace QR payload: {payload}");

            AppServices.Qr.GenerateQrCode(payload);
        }

        private async Task PrepareBootstrapConfigAndQrAsync(ConfigModel bootstrapConfig)
        {
            if (string.IsNullOrWhiteSpace(bootstrapConfig.DeviceId))
            {
                bootstrapConfig.DeviceId = GenerateId();
            }

            if (string.IsNullOrWhiteSpace(bootstrapConfig.KeyContainer))
            {
                bootstrapConfig.KeyContainer = "KeyContainer_" + bootstrapConfig.DeviceId;
            }

            if (string.IsNullOrWhiteSpace(bootstrapConfig.PublicKeyDER))
            {
                await Task.Run(() =>
                {
                    new Pki().CreatePrivateKey(bootstrapConfig.KeyContainer);
                    bootstrapConfig.PublicKeyDER =
                        new Pki().GetPublicKeyDerBase64(bootstrapConfig.KeyContainer);
                }).ConfigureAwait(false);
            }

            _configStore.SaveBootstrapConfig(bootstrapConfig);
            AppServices.Qr.GenerateQrCode(CreateJsonToQrCode(bootstrapConfig));
        }

        private async Task<DesktopRegistration?> PollRegistrationApi(string? baseUrlOverride, CancellationTokenSource registrationCts)
        {
            var httpApiClient = new HttpApiClient();
            PendingRegistrationContext? pendingRegistration = !string.IsNullOrWhiteSpace(baseUrlOverride)
                ? _pendingServerRegistration
                : null;
            int attempt = 0;
            CancellationToken registrationToken = registrationCts.Token;

            string deviceId;

            if (pendingRegistration != null)
            {
                deviceId = pendingRegistration.DeviceId;
                Debug.WriteLine(
                    $"[REG] Namespace registration polling prepared. deviceId='{pendingRegistration.DeviceId}', namespaceId='{pendingRegistration.NamespaceId}', namespaceUrl='{pendingRegistration.NamespaceUrl}'.");
            }
            else
            {
                ConfigModel bootstrapConfig = _configStore.LoadBootstrapConfig();
                deviceId = bootstrapConfig.DeviceId;
                Debug.WriteLine($"[REG] Root registration polling prepared. deviceId='{deviceId}'.");
            }

            DesktopRegistration? registration = null;
            string registrationPayload = JsonSerializer.Serialize(new
            {
                deviceId
            });

            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                deviceId,
                registrationPayload,
                "registration polling request");
            string endpoint = BuildRegistrationApiUrl(deviceId, baseUrlOverride);

            if (pendingRegistration != null)
            {
                string signaturePreview = headers.TryGetValue("x-signature", out string? signature)
                    ? $"len={signature.Length}"
                    : "missing";

                Debug.WriteLine($"[NS-ADD] Polling signature payload: {registrationPayload}");
                Debug.WriteLine($"[NS-ADD] Polling signature header: {signaturePreview}");
            }

            Debug.WriteLine($"Registration polling endpoint: {endpoint}");
            Debug.WriteLine($"Starting registration polling for deviceId: {deviceId}");
            Debug.WriteLine($"Registration payload: {registrationPayload}");


            while (!registrationToken.IsCancellationRequested)
            {
                try
                {
                    attempt++;
                    Debug.WriteLine($"[REG] Poll attempt #{attempt} started for deviceId='{deviceId}'.");

                    await httpApiClient.ConnectToSseEndpoint(
                        endpoint,
                        headers,
                        message =>
                        {
                            Debug.WriteLine($"[REG] SSE raw message: {message}");

                            var parsed = Transformer.ToRegistrationResponse(message);

                            bool isValidRegistration =
                                parsed != null &&
                                parsed.Id > 0 &&
                                !string.IsNullOrWhiteSpace(parsed.AccountId) &&
                                !string.IsNullOrWhiteSpace(parsed.DeviceId) &&
                                !string.IsNullOrWhiteSpace(parsed.EncDek);

                            if (!isValidRegistration)
                            {
                                Debug.WriteLine(
                                    $"[REG] SSE parsed but invalid. id='{parsed?.Id}', accountId='{parsed?.AccountId}', deviceId='{parsed?.DeviceId}', hasEncDek={!string.IsNullOrWhiteSpace(parsed?.EncDek)}.");
                                Debug.WriteLine($"SSE ignored: {message}");
                                return;
                            }

                            Debug.WriteLine(
                                $"[REG] Valid registration received from SSE. id='{parsed!.Id}', accountId='{parsed.AccountId}', deviceId='{parsed.DeviceId}', namespaces={parsed.Namespaces.Count}.");
                            registration = parsed;
                            registrationCts.Cancel();
                        },
                        registrationToken).ConfigureAwait(false);

                    Debug.WriteLine($"[REG] SSE connection completed without exception for deviceId='{deviceId}'.");
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("SSE polling cancelled.");
                    break;
                }
                catch (HttpRequestException ex) when (IsRetryableRegistrationFailure(ex))
                {
                    Debug.WriteLine($"Registration polling retry: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5), registrationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Registration polling failed: {ex}");
                    RegistrationFailed?.Invoke(ex.Message);
                    break;
                }
            }

            if (registration != null)
            {
                if (pendingRegistration != null)
                {
                    Debug.WriteLine(
                        $"[REG] Starting namespace resolve after SSE. deviceId='{pendingRegistration.DeviceId}', namespaceId='{pendingRegistration.NamespaceId}'.");
                    string resolvedAccountId = await ResolveNamespaceAccountIdAsync(pendingRegistration).ConfigureAwait(false);
                    Debug.WriteLine(
                        $"[REG] Namespace resolve succeeded. namespaceId='{pendingRegistration.NamespaceId}', accountId='{resolvedAccountId}'.");
                    _configStore.UpdateNamespaceAccountId(pendingRegistration.NamespaceId, resolvedAccountId);
                    Debug.WriteLine(
                        $"[REG] Preserving existing root registration after namespace add. SSE registration for deviceId='{registration.DeviceId}' will not overwrite config root fields.");
                }

                _pendingServerRegistration = null;

                if (pendingRegistration == null)
                {
                    _configStore.SaveRegistration(registration);
                }

                RegistrationCompleted?.Invoke();
                Debug.WriteLine($"[REG] Registration flow completed for deviceId='{registration.DeviceId}'.");
            }
            else
            {
                Debug.WriteLine($"[REG] Polling loop exited without valid registration for deviceId='{deviceId}'.");
            }

            return registration;
        }

        private static async Task<string> ResolveNamespaceAccountIdAsync(PendingRegistrationContext pendingRegistration)
        {
            var httpApiClient = new HttpApiClient();
            string resolvePayload = JsonSerializer.Serialize(new
            {
                deviceId = pendingRegistration.DeviceId,
                namespaceId = pendingRegistration.NamespaceId
            });

            IReadOnlyDictionary<string, string> resolveHeaders = BrowserCommandHandler.CreateSignatureHeaders(
                pendingRegistration.DeviceId,
                resolvePayload,
                "namespace resolve request");

            string resolveSignaturePreview = resolveHeaders.TryGetValue("x-signature", out string? resolveSignature)
                ? $"len={resolveSignature.Length}"
                : "missing";

            string resolveEndpoint = $"{pendingRegistration.NamespaceUrl}{ApiRoutes.VersionPrefix}/{ApiRoutes.ResolveNamespace(pendingRegistration.DeviceId, pendingRegistration.NamespaceId)}";

            Debug.WriteLine($"[REG] Resolve endpoint: {resolveEndpoint}");
            Debug.WriteLine($"[REG] Resolve payload: {resolvePayload}");
            Debug.WriteLine($"[NS-ADD] Resolve signature header: {resolveSignaturePreview}");

            ResolveNamespaceResponseDto? resolveResponse = await httpApiClient
                .GetJsonAsync<ResolveNamespaceResponseDto>(resolveEndpoint, resolveHeaders)
                .ConfigureAwait(false);

            Debug.WriteLine(
                $"[NS-ADD] Resolve response parsed. accountId='{resolveResponse?.AccountId ?? "<null>"}'.");

            if (resolveResponse == null || string.IsNullOrWhiteSpace(resolveResponse.AccountId))
            {
                throw new InvalidDataException("Resolve namespace request did not return a valid accountId.");
            }

            return resolveResponse.AccountId;
        }

        private static string BuildRegistrationApiUrl(string deviceId, string? baseUrlOverride)
        {
            if (string.IsNullOrWhiteSpace(baseUrlOverride))
            {
                return AppEnvironment.BuildApiUrl(ApiRoutes.RootDevice(deviceId));
            }

            return $"{NormalizeBaseUrl(baseUrlOverride)}{ApiRoutes.VersionPrefix}/{ApiRoutes.RootDevice(deviceId)}";
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("Base URL must not be empty.");
            }

            return baseUrl.Trim().TrimEnd('/');
        }

        private static string NormalizeNamespaceName(string namespaceName)
        {
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                throw new InvalidOperationException("Namespace name must not be empty.");
            }

            return namespaceName.Trim();
        }

        private static bool IsRetryableRegistrationFailure(HttpRequestException exception)
        {
            return exception.Message.Contains("Device not found for signature verification", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("e_40101_2", StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateNamespaceId(string namespaceName, string namespaceUrl)
        {
            byte[] seedBytes = Encoding.UTF8.GetBytes($"{namespaceName}\n{namespaceUrl}");
            byte[] hashBytes = SHA256.HashData(seedBytes);

            return Convert.ToHexString(hashBytes[..5]).ToLowerInvariant();
        }

        private static string CreateJsonToQrCode(ConfigModel bootstrapConfig)
        {
            var data = new
            {
                type = "deviceRegistration",
                deviceId = bootstrapConfig.DeviceId,
                publicKey = bootstrapConfig.PublicKeyDER,
                deviceName = Environment.MachineName
            };

            return JsonSerializer.Serialize(data);
        }

        private static string GenerateId(int length = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(
                Enumerable.Range(0, length)
                    .Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)])
                    .ToArray());
        }

        private sealed record PendingRegistrationContext(
            string DeviceId,
            string NamespaceId,
            string NamespaceUrl);
    }
}