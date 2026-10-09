using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    public class BootstrapRegistrationService
    {
        private const string NamespaceRegistrationType = "namespace";
        private const string NamespaceSyncRegistrationType = "namespace_sync";

        private readonly RegistrationConfigStore _configStore = new();
        private readonly QrService _qrService = new();
        private CancellationTokenSource _registrationCts = new();
        private PendingRootRegistrationContext? _pendingRootRegistration;
        private PendingRegistrationContext? _pendingServerRegistration;

        public event Action? RegistrationCompleted;
        public event Action<string>? RegistrationFailed;

        internal Bitmap? QrCode => _qrService.GetQrCode();

        public bool LoadOrCreateConfig()
        {
            if (_configStore.TryReadRegistrations(out List<DesktopRegistration>? registrations) &&
                registrations is { Count: > 0 })
            {
                return true;
            }

            return false;
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

        public void CancelRegistrationPolling()
        {
            if (!_registrationCts.IsCancellationRequested)
            {
                _registrationCts.Cancel();
            }
        }

        public string GetBootstrapBaseUrl()
        {
            return _pendingRootRegistration?.BaseUrl ?? _configStore.LoadBootstrapConfig().BaseUrl;
        }

        public async Task PrepareRegistrationQrAsync(string baseUrl, string userName)
        {
            _pendingServerRegistration = null;
            _pendingRootRegistration = null;

            string pendingUserId = Guid.NewGuid().ToString("D");
            string normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
            EnsureUrlIsNotAlreadyRegistered(normalizedBaseUrl, pendingUserId);
            await EnsureServerIsHealthyAsync(normalizedBaseUrl).ConfigureAwait(false);

            ConfigModel bootstrapConfig = new()
            {
                BaseUrl = normalizedBaseUrl
            };

            string normalizedUserName = NormalizeUserName(userName);

            await PrepareBootstrapConfigAndQrAsync(bootstrapConfig).ConfigureAwait(false);

            _pendingRootRegistration = new PendingRootRegistrationContext(
                pendingUserId,
                normalizedUserName,
                bootstrapConfig.BaseUrl,
                bootstrapConfig.DesktopDeviceId);
        }

        public async Task PrepareServerRegistrationQrAsync(string namespaceUrl)
        {
            await PrepareNamespaceRegistrationQrAsync(namespaceUrl, namespaceName: null, NamespaceSyncRegistrationType).ConfigureAwait(false);
        }

        public async Task PrepareNewServerRegistrationQrAsync(string namespaceUrl, string namespaceName)
        {
            await PrepareNamespaceRegistrationQrAsync(namespaceUrl, NormalizeNamespaceName(namespaceName), NamespaceRegistrationType).ConfigureAwait(false);
        }

        private async Task PrepareNamespaceRegistrationQrAsync(string namespaceUrl, string? namespaceName, string registrationType)
        {
            DesktopRegistration activeRegistration = _configStore.ReadRegistration(AppEnvironment.GetActiveUserId());
            var pki = new Pki();
            string desktopDeviceId = GenerateId();
            string keyContainer = "KeyContainer_" + desktopDeviceId;
            string normalizedNamespaceUrl = NormalizeBaseUrl(namespaceUrl);
            EnsureUrlIsNotAlreadyRegistered(normalizedNamespaceUrl, activeRegistration.UserKey);
            await EnsureServerIsHealthyAsync(normalizedNamespaceUrl).ConfigureAwait(false);
            string namespaceId = CreateNamespaceId(namespaceName, normalizedNamespaceUrl);

            LogNamespaceRegistration(
                $"Preparing namespace QR. type='{registrationType}', desktopDeviceId='{desktopDeviceId}', namespaceId='{namespaceId}', namespaceUrl='{normalizedNamespaceUrl}', namespaceName='{namespaceName ?? "<none>"}'.");

            pki.CreatePrivateKey(keyContainer);
            string publicKey = pki.GetPublicKeyDerBase64(keyContainer);

            LogNamespaceRegistration($"Namespace keypair ready. keyContainer='{keyContainer}', publicKeyLength={publicKey.Length}.");

            _pendingServerRegistration = new PendingRegistrationContext(
                activeRegistration.UserKey,
                desktopDeviceId,
                namespaceId,
                normalizedNamespaceUrl,
                namespaceName ?? normalizedNamespaceUrl);

            object payloadData = namespaceName == null
                ? new
                {
                    type = registrationType,
                    desktopDeviceId,
                    publicKey,
                    deviceName = Environment.MachineName,
                    namespaceId,
                    namespaceUrl = normalizedNamespaceUrl
                }
                : new
                {
                    type = registrationType,
                    desktopDeviceId,
                    publicKey,
                    deviceName = Environment.MachineName,
                    namespaceId,
                    namespaceUrl = normalizedNamespaceUrl,
                    namespaceName
                };
            string payload = JsonSerializer.Serialize(payloadData);

            LogNamespaceRegistration($"Namespace QR payload: {payload}");

            _qrService.GenerateQrCode(payload);
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

            _qrService.GenerateQrCode(CreateJsonToQrCode(bootstrapConfig));
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
                desktopDeviceId = pendingRegistration.DeviceId;
                LogRegistration(
                    $"Namespace registration polling prepared. desktopDeviceId='{pendingRegistration.DeviceId}', namespaceId='{pendingRegistration.NamespaceId}', namespaceUrl='{pendingRegistration.NamespaceUrl}'.");
            }
            else if (pendingRootRegistration != null)
            {
                desktopDeviceId = pendingRootRegistration.DeviceId;
                LogRegistration($"Root registration polling prepared from pending context. desktopDeviceId='{desktopDeviceId}'.");
            }
            else
            {
                ConfigModel bootstrapConfig = _configStore.LoadBootstrapConfig();
                desktopDeviceId = bootstrapConfig.DesktopDeviceId;
                LogRegistration($"Root registration polling prepared. desktopDeviceId='{desktopDeviceId}'.");
            }

            DesktopRegistration? registration = null;
            string nonce = NonceGenerator.Create();
            string registrationPayload = JsonSerializer.Serialize(new
            {
                desktopDeviceId,
                nonce
            });

            IReadOnlyDictionary<string, string> headers = RequestSigner.CreateSignatureHeaders(
                desktopDeviceId,
                registrationPayload,
                "registration polling request");
            string endpoint = BuildRegistrationApiUrl(desktopDeviceId, nonce, pendingRootRegistration?.BaseUrl ?? baseUrlOverride);

            if (pendingRegistration != null)
            {
                string signaturePreview = headers.TryGetValue("x-signature", out string? signature)
                    ? $"len={signature.Length}"
                    : "missing";

                LogNamespaceRegistration($"Polling signature payload: {registrationPayload}");
                LogNamespaceRegistration($"Polling signature header: {signaturePreview}");
            }

            LogRegistration($"Registration polling endpoint: {endpoint}");
            LogRegistration($"Starting registration polling for desktopDeviceId: {desktopDeviceId}");
            LogRegistration($"Registration payload: {registrationPayload}");


            while (!registrationToken.IsCancellationRequested)
            {
                try
                {
                    attempt++;
                    LogRegistration($"Poll attempt #{attempt} started for desktopDeviceId='{desktopDeviceId}'.");

                    await httpApiClient.ConnectToSseEndpoint(
                        endpoint,
                        headers,
                        message =>
                        {
                            LogRegistration($"SSE raw message: {message}");

                            var parsed = RegistrationConfigStore.ParseRegistrationResponse(message);

                            bool isValidRegistration =
                                parsed != null &&
                                parsed.Id > 0 &&
                                !string.IsNullOrWhiteSpace(parsed.AccountId) &&
                                !string.IsNullOrWhiteSpace(parsed.DesktopDeviceId) &&
                                !string.IsNullOrWhiteSpace(parsed.EncDek);

                            if (!isValidRegistration)
                            {
                                LogRegistration(
                                    $"SSE parsed but invalid. id='{parsed?.Id}', accountId='{parsed?.AccountId}', desktopDeviceId='{parsed?.DesktopDeviceId}', hasEncDek={!string.IsNullOrWhiteSpace(parsed?.EncDek)}.");
                                LogRegistration($"SSE ignored: {message}");
                                return;
                            }

                            LogRegistration(
                                $"Valid registration received from SSE. id='{parsed!.Id}', accountId='{parsed.AccountId}', desktopDeviceId='{parsed.DesktopDeviceId}', namespaces={parsed.Namespaces.Count}.");
                            registration = parsed;
                            registrationCts.Cancel();
                        },
                        registrationToken).ConfigureAwait(false);

                    LogRegistration($"SSE connection completed without exception for desktopDeviceId='{desktopDeviceId}'.");
                }
                catch (OperationCanceledException)
                {
                    LogRegistration("SSE polling cancelled.");
                    break;
                }
                catch (HttpRequestException ex) when (IsRetryableRegistrationFailure(ex))
                {
                    LogRegistration($"Registration polling retry: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5), registrationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogRegistration($"Registration polling failed: {ex}");
                    RegistrationFailed?.Invoke(ex.Message);
                    break;
                }
            }

            if (registration != null)
            {
                try
                {
                    await FinalizeRegistrationAsync(registration, pendingRegistration, pendingRootRegistration).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogRegistration($"Registration finalization failed: {ex}");
                    RegistrationFailed?.Invoke(ex.Message);
                }
            }
            else
            {
                LogRegistration($"Polling loop exited without valid registration for desktopDeviceId='{desktopDeviceId}'.");
            }

            return registration;
        }

        private async Task FinalizeRegistrationAsync(
            DesktopRegistration registration,
            PendingRegistrationContext? pendingRegistration,
            PendingRootRegistrationContext? pendingRootRegistration)
        {
            if (pendingRegistration != null)
            {
                LogRegistration(
                    $"Starting namespace resolve after SSE. desktopDeviceId='{pendingRegistration.DeviceId}', namespaceId='{pendingRegistration.NamespaceId}'.");
                ResolveNamespaceResponseDto resolvedNamespace = await ResolveNamespaceAsync(pendingRegistration).ConfigureAwait(false);
                LogRegistration(
                    $"Namespace resolve succeeded. requestedNamespaceId='{pendingRegistration.NamespaceId}', resolvedNamespaceId='{resolvedNamespace.NamespaceId}', accountId='{resolvedNamespace.AccountId}'.");
                _configStore.UpsertNamespace(pendingRegistration.UserId, new NamespaceItemDto
                {
                    DesktopDeviceId = pendingRegistration.DeviceId,
                    DeviceName = Environment.MachineName,
                    NamespaceId = pendingRegistration.NamespaceId,
                    Namespace = pendingRegistration.NamespaceName,
                    Url = pendingRegistration.NamespaceUrl
                });
                _configStore.UpdateNamespaceRegistration(
                    pendingRegistration.UserId,
                    pendingRegistration.NamespaceId,
                    resolvedNamespace.NamespaceId,
                    resolvedNamespace.AccountId,
                    resolvedNamespace.NamespaceName,
                    registration.EncDek);
                LogRegistration(
                    $"Preserving existing root registration after namespace add. SSE registration for desktopDeviceId='{registration.DeviceId}' will not overwrite config root fields.");
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
            LogRegistration($"Registration flow completed for desktopDeviceId='{registration.DeviceId}'.");
        }

        private static async Task<ResolveNamespaceResponseDto> ResolveNamespaceAsync(PendingRegistrationContext pendingRegistration)
        {
            var httpApiClient = new HttpApiClient();
            string nonce = NonceGenerator.Create();
            string resolvePayload = JsonSerializer.Serialize(new
            {
                desktopDeviceId = pendingRegistration.DeviceId,
                nonce
            });

            IReadOnlyDictionary<string, string> resolveHeaders = RequestSigner.CreateSignatureHeaders(
                pendingRegistration.DeviceId,
                resolvePayload,
                "namespace resolve request");

            string resolveSignaturePreview = resolveHeaders.TryGetValue("x-signature", out string? resolveSignature)
                ? $"len={resolveSignature.Length}"
                : "missing";

            string resolveEndpoint = $"{pendingRegistration.NamespaceUrl}{ApiRoutes.VersionPrefix}/{ApiRoutes.ResolveNamespace(pendingRegistration.DeviceId, nonce)}";

            LogRegistration($"Resolve endpoint: {resolveEndpoint}");
            LogRegistration($"Resolve payload: {resolvePayload}");
            LogNamespaceRegistration($"Resolve signature header: {resolveSignaturePreview}");

            ResolveNamespaceResponseDto? resolveResponse = await httpApiClient
                .GetJsonAsync<ResolveNamespaceResponseDto>(resolveEndpoint, resolveHeaders)
                .ConfigureAwait(false);

            LogNamespaceRegistration(
                $"Resolve response parsed. namespaceId='{resolveResponse?.NamespaceId ?? "<null>"}', accountId='{resolveResponse?.AccountId ?? "<null>"}', namespaceName='{resolveResponse?.NamespaceName ?? "<null>"}'.");

            if (resolveResponse == null ||
                string.IsNullOrWhiteSpace(resolveResponse.NamespaceId) ||
                string.IsNullOrWhiteSpace(resolveResponse.AccountId))
            {
                throw new InvalidDataException("Resolve namespace request did not return a valid namespace response.");
            }

            return resolveResponse;
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

        private static string NormalizeUserName(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new InvalidOperationException("User name must not be empty.");
            }

            return userName.Trim();
        }

        private static async Task EnsureServerIsHealthyAsync(string normalizedBaseUrl)
        {
            string endpoint = $"{normalizedBaseUrl}{ApiRoutes.VersionPrefix}/{ApiRoutes.Healthy}";

            try
            {
                var httpApiClient = new HttpApiClient(TimeSpan.FromSeconds(10));
                await httpApiClient.GetAsync(endpoint).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"The server at '{normalizedBaseUrl}' is unavailable or unhealthy.",
                    exception);
            }
        }

        private void EnsureUrlIsNotAlreadyRegistered(string normalizedUrl, string? userKey)
        {
            if (_configStore.ContainsConfiguredUrl(normalizedUrl, userKey))
            {
                throw new InvalidOperationException($"The URL '{normalizedUrl}' already exists in config.json.");
            }
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

        private static string CreateNamespaceId(string? namespaceName, string namespaceUrl)
        {
            byte[] seedBytes = Encoding.UTF8.GetBytes($"{namespaceName ?? string.Empty}|{namespaceUrl}");
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
                deviceName = Environment.MachineName,
                targetUrl = bootstrapConfig.BaseUrl
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

        private static void LogRegistration(string message)
        {
            Debug.WriteLine($"[REG] {message}");
        }

        private static void LogNamespaceRegistration(string message)
        {
            Debug.WriteLine($"[NS-ADD] {message}");
        }

        private sealed record PendingRegistrationContext(
            string UserId,
            string DeviceId,
            string NamespaceId,
            string NamespaceUrl,
            string NamespaceName);

        private sealed record PendingRootRegistrationContext(
            string UserId,
            string UserName,
            string BaseUrl,
            string DeviceId);
    }
}