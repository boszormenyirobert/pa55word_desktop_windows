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
        private PendingRootRegistrationContext? _pendingRootRegistration;
        private PendingRegistrationContext? _pendingServerRegistration;

        public event Action? RegistrationCompleted;
        public event Action<string>? RegistrationFailed;

        public Task<bool> LoadOrCreateConfig()
        {
            if (_configStore.TryReadRegistrations(out List<DesktopRegistration>? registrations) &&
                registrations is { Count: > 0 })
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
            return _pendingRootRegistration?.BaseUrl ?? _configStore.LoadBootstrapConfig().BaseUrl;
        }

        public async Task PrepareRegistrationQrAsync(string baseUrl, string userName)
        {
            _pendingServerRegistration = null;
            _pendingRootRegistration = null;

            ConfigModel bootstrapConfig = new()
            {
                BaseUrl = NormalizeBaseUrl(baseUrl)
            };

            string normalizedUserName = NormalizeUserName(userName);

            await PrepareBootstrapConfigAndQrAsync(bootstrapConfig).ConfigureAwait(false);

            _pendingRootRegistration = new PendingRootRegistrationContext(
                Guid.NewGuid().ToString("D"),
                normalizedUserName,
                bootstrapConfig.BaseUrl,
                bootstrapConfig.DesktopDeviceId);
        }

        public async Task PrepareServerRegistrationQrAsync(string namespaceUrl, string namespaceName)
        {
            DesktopRegistration activeRegistration = _configStore.ReadRegistration(AppEnvironment.GetActiveUserId());
            var pki = new Pki();
            string desktopDeviceId = GenerateId();
            string keyContainer = "KeyContainer_" + desktopDeviceId;
            string normalizedNamespaceUrl = NormalizeBaseUrl(namespaceUrl);
            string normalizedNamespaceName = NormalizeNamespaceName(namespaceName);
            string namespaceId = CreateNamespaceId(normalizedNamespaceName, normalizedNamespaceUrl);

            Debug.WriteLine(
                $"[NS-ADD] Preparing namespace QR. desktopDeviceId='{desktopDeviceId}', namespaceId='{namespaceId}', namespaceUrl='{normalizedNamespaceUrl}', namespaceName='{normalizedNamespaceName}'.");

            pki.CreatePrivateKey(keyContainer);
            string publicKey = pki.GetPublicKeyDerBase64(keyContainer);

            Debug.WriteLine($"[NS-ADD] Namespace keypair ready. keyContainer='{keyContainer}', publicKeyLength={publicKey.Length}.");

            _pendingServerRegistration = new PendingRegistrationContext(
                activeRegistration.UserKey,
                desktopDeviceId,
                namespaceId,
                normalizedNamespaceUrl);

            _configStore.UpsertNamespace(activeRegistration.UserKey, new NamespaceItemDto
            {
                DesktopDeviceId = desktopDeviceId,
                DeviceName = Environment.MachineName,
                NamespaceId = namespaceId,
                Namespace = normalizedNamespaceName,
                Url = normalizedNamespaceUrl
            });

            string payload = JsonSerializer.Serialize(new
            {
                type = "namespace",
                desktopDeviceId,
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
            if (string.IsNullOrWhiteSpace(bootstrapConfig.DesktopDeviceId))
            {
                bootstrapConfig.DesktopDeviceId = GenerateId();
            }

            if (string.IsNullOrWhiteSpace(bootstrapConfig.KeyContainer))
            {
                bootstrapConfig.KeyContainer = "KeyContainer_" + bootstrapConfig.DesktopDeviceId;
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
            PendingRootRegistrationContext? pendingRootRegistration = string.IsNullOrWhiteSpace(baseUrlOverride)
                ? _pendingRootRegistration
                : null;
            int attempt = 0;
            CancellationToken registrationToken = registrationCts.Token;

            string desktopDeviceId;

            if (pendingRegistration != null)
            {
                desktopDeviceId = pendingRegistration.DesktopDeviceId;
                Debug.WriteLine(
                    $"[REG] Namespace registration polling prepared. desktopDeviceId='{pendingRegistration.DesktopDeviceId}', namespaceId='{pendingRegistration.NamespaceId}', namespaceUrl='{pendingRegistration.NamespaceUrl}'.");
            }
            else if (pendingRootRegistration != null)
            {
                desktopDeviceId = pendingRootRegistration.DesktopDeviceId;
                Debug.WriteLine($"[REG] Root registration polling prepared from pending context. desktopDeviceId='{desktopDeviceId}'.");
            }
            else
            {
                ConfigModel bootstrapConfig = _configStore.LoadBootstrapConfig();
                desktopDeviceId = bootstrapConfig.DesktopDeviceId;
                Debug.WriteLine($"[REG] Root registration polling prepared. desktopDeviceId='{desktopDeviceId}'.");
            }

            DesktopRegistration? registration = null;
            string nonce = NonceGenerator.Create();
            string registrationPayload = JsonSerializer.Serialize(new
            {
                desktopDeviceId,
                nonce
            });

            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(
                desktopDeviceId,
                registrationPayload,
                "registration polling request");
            string endpoint = BuildRegistrationApiUrl(desktopDeviceId, nonce, pendingRootRegistration?.BaseUrl ?? baseUrlOverride);

            if (pendingRegistration != null)
            {
                string signaturePreview = headers.TryGetValue("x-signature", out string? signature)
                    ? $"len={signature.Length}"
                    : "missing";

                Debug.WriteLine($"[NS-ADD] Polling signature payload: {registrationPayload}");
                Debug.WriteLine($"[NS-ADD] Polling signature header: {signaturePreview}");
            }

            Debug.WriteLine($"Registration polling endpoint: {endpoint}");
            Debug.WriteLine($"Starting registration polling for desktopDeviceId: {desktopDeviceId}");
            Debug.WriteLine($"Registration payload: {registrationPayload}");


            while (!registrationToken.IsCancellationRequested)
            {
                try
                {
                    attempt++;
                    Debug.WriteLine($"[REG] Poll attempt #{attempt} started for desktopDeviceId='{desktopDeviceId}'.");

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
                                !string.IsNullOrWhiteSpace(parsed.DesktopDeviceId) &&
                                !string.IsNullOrWhiteSpace(parsed.EncDek);

                            if (!isValidRegistration)
                            {
                                Debug.WriteLine(
                                    $"[REG] SSE parsed but invalid. id='{parsed?.Id}', accountId='{parsed?.AccountId}', desktopDeviceId='{parsed?.DesktopDeviceId}', hasEncDek={!string.IsNullOrWhiteSpace(parsed?.EncDek)}.");
                                Debug.WriteLine($"SSE ignored: {message}");
                                return;
                            }

                            Debug.WriteLine(
                                $"[REG] Valid registration received from SSE. id='{parsed!.Id}', accountId='{parsed.AccountId}', desktopDeviceId='{parsed.DesktopDeviceId}', namespaces={parsed.Namespaces.Count}.");
                            registration = parsed;
                            registrationCts.Cancel();
                        },
                        registrationToken).ConfigureAwait(false);

                    Debug.WriteLine($"[REG] SSE connection completed without exception for desktopDeviceId='{desktopDeviceId}'.");
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
                try
                {
                    if (pendingRegistration != null)
                    {
                        Debug.WriteLine(
                            $"[REG] Starting namespace resolve after SSE. desktopDeviceId='{pendingRegistration.DesktopDeviceId}', namespaceId='{pendingRegistration.NamespaceId}'.");
                        string resolvedAccountId = await ResolveNamespaceAccountIdAsync(pendingRegistration).ConfigureAwait(false);
                        Debug.WriteLine(
                            $"[REG] Namespace resolve succeeded. namespaceId='{pendingRegistration.NamespaceId}', accountId='{resolvedAccountId}'.");
                            Debug.WriteLine(
                                $"[NS-ADD] Updating config namespace account. userId='{pendingRegistration.UserId}', namespaceId='{pendingRegistration.NamespaceId}', accountId='{resolvedAccountId}'.");
                        _configStore.UpdateNamespaceAccountId(pendingRegistration.UserId, pendingRegistration.NamespaceId, resolvedAccountId);
                            Debug.WriteLine(
                                $"[NS-ADD] Config namespace account updated. userId='{pendingRegistration.UserId}', namespaceId='{pendingRegistration.NamespaceId}', accountId='{resolvedAccountId}'.");
                        Debug.WriteLine(
                            $"[REG] Preserving existing root registration after namespace add. SSE registration for desktopDeviceId='{registration.DesktopDeviceId}' will not overwrite config root fields.");
                    }

                    _pendingRootRegistration = null;
                    _pendingServerRegistration = null;

                    if (pendingRegistration == null)
                    {
                        if (pendingRootRegistration != null)
                        {
                            registration.UserId = pendingRootRegistration.UserId;
                            registration.UserName = pendingRootRegistration.UserName;
                        }

                        _configStore.SaveRegistration(registration);
                        AppEnvironment.SetActiveUser(registration.UserKey);
                    }

                    RegistrationCompleted?.Invoke();
                    Debug.WriteLine($"[REG] Registration flow completed for desktopDeviceId='{registration.DesktopDeviceId}'.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[REG] Registration finalization failed: {ex}");
                    RegistrationFailed?.Invoke(ex.Message);
                }
            }
            else
            {
                Debug.WriteLine($"[REG] Polling loop exited without valid registration for desktopDeviceId='{desktopDeviceId}'.");
            }

            return registration;
        }

        private static async Task<string> ResolveNamespaceAccountIdAsync(PendingRegistrationContext pendingRegistration)
        {
            var httpApiClient = new HttpApiClient();
            string nonce = NonceGenerator.Create();
            string resolvePayload = JsonSerializer.Serialize(new
            {
                desktopDeviceId = pendingRegistration.DesktopDeviceId,
                namespaceId = pendingRegistration.NamespaceId,
                nonce
            });

            IReadOnlyDictionary<string, string> resolveHeaders = BrowserCommandHandler.CreateSignatureHeaders(
                pendingRegistration.DesktopDeviceId,
                resolvePayload,
                "namespace resolve request");

            string resolveSignaturePreview = resolveHeaders.TryGetValue("x-signature", out string? resolveSignature)
                ? $"len={resolveSignature.Length}"
                : "missing";

            string resolveEndpoint = $"{pendingRegistration.NamespaceUrl}{ApiRoutes.VersionPrefix}/{ApiRoutes.ResolveNamespace(pendingRegistration.DesktopDeviceId, pendingRegistration.NamespaceId, nonce)}";

                Debug.WriteLine(
                    $"[NS-ADD] Resolve request prepared. userId='{pendingRegistration.UserId}', desktopDeviceId='{pendingRegistration.DesktopDeviceId}', namespaceId='{pendingRegistration.NamespaceId}', namespaceUrl='{pendingRegistration.NamespaceUrl}'.");
                Debug.WriteLine($"[REG] Resolve endpoint: {resolveEndpoint}");
                Debug.WriteLine($"[REG] Resolve payload: {resolvePayload}");
            Debug.WriteLine($"[NS-ADD] Resolve signature header: {resolveSignaturePreview}");

            ResolveNamespaceResponseDto? resolveResponse = await httpApiClient
                .GetJsonAsync<ResolveNamespaceResponseDto>(resolveEndpoint, resolveHeaders)
                .ConfigureAwait(false);

            Debug.WriteLine(
                    $"[NS-ADD] Resolve response parsed. userId='{pendingRegistration.UserId}', desktopDeviceId='{pendingRegistration.DesktopDeviceId}', namespaceId='{pendingRegistration.NamespaceId}', accountId='{resolveResponse?.AccountId ?? "<null>"}'.");

            if (resolveResponse == null || string.IsNullOrWhiteSpace(resolveResponse.AccountId))
            {
                    Debug.WriteLine(
                    $"[NS-ADD] Resolve response missing accountId. userId='{pendingRegistration.UserId}', desktopDeviceId='{pendingRegistration.DesktopDeviceId}', namespaceId='{pendingRegistration.NamespaceId}'.");
                throw new InvalidDataException("Resolve namespace request did not return a valid accountId.");
            }

            return resolveResponse.AccountId;
        }

        private static string BuildRegistrationApiUrl(string desktopDeviceId, string nonce, string? baseUrlOverride)
        {
            if (string.IsNullOrWhiteSpace(baseUrlOverride))
            {
                return AppEnvironment.BuildApiUrl(ApiRoutes.RootDevice(desktopDeviceId, nonce));
            }

            return $"{NormalizeBaseUrl(baseUrlOverride)}{ApiRoutes.VersionPrefix}/{ApiRoutes.RootDevice(desktopDeviceId, nonce)}";
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

        private static string NormalizeUserName(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new InvalidOperationException("User name must not be empty.");
            }

            return userName.Trim();
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
                desktopDeviceId = bootstrapConfig.DesktopDeviceId,
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
            string UserId,
            string DesktopDeviceId,
            string NamespaceId,
            string NamespaceUrl);

        private sealed record PendingRootRegistrationContext(
            string UserId,
            string UserName,
            string BaseUrl,
            string DesktopDeviceId);
    }
}