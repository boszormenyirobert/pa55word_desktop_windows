using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class BrowserCommandHandler : INativeMessageProcessor
    {
        private static string ConnectionEndpoint => AppEnvironment.BuildApiUrl(ApiRoutes.CredentialStore);
        private static string ConnectionReadEndpoint => AppEnvironment.BuildApiUrl(ApiRoutes.CredentialStoreAll);
        
        private readonly RegisteredDeviceService _registeredDeviceService;
        private readonly HttpApiClient _httpApiClient;
        private readonly AuthenticationService _authenticationService;

        private readonly Action<string>? _log;        

        public BrowserCommandHandler(
            RegisteredDeviceService registeredDeviceService,
            HttpApiClient httpApiClient,
            AuthenticationService authenticationService,
            Action<string>? log = null)
        {
            _registeredDeviceService = registeredDeviceService;
            _httpApiClient = httpApiClient;
            _authenticationService = authenticationService;
            _log = log;
        }

        public async Task<string> ProcessRequestAsync(string requestJson)
        {
            BrowserExtensionPayloadDto? payload;

            try
            {
                payload = JsonSerializer.Deserialize<BrowserExtensionPayloadDto>(
                    requestJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch (JsonException ex)
            {
                return CreateErrorResponseJson("parse_error", ex.Message);
            }

            if (payload == null)
            {
                return CreateErrorResponseJson("parse_error", "Payload is empty or invalid JSON.");
            }

            try
            {
                bool authorized = await _authenticationService.EnsureAuthorizedAsync().ConfigureAwait(false);
                if (!authorized)
                {
                    return CreateErrorResponseJson("authorization_declined", "User authorization was declined or not confirmed.");
                }

                if (string.Equals(payload.Rest, MessagingAction.Read, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleReadAsync().ConfigureAwait(false);
                }

                if (string.Equals(payload.Rest, MessagingAction.Create, StringComparison.OrdinalIgnoreCase))
                {
                    await HandleCreateAsync(payload).ConfigureAwait(false);
                    return JsonSerializer.Serialize(new { ok = true });
                }

                if (string.Equals(payload.Rest, MessagingAction.Update, StringComparison.OrdinalIgnoreCase))
                {
                    await HandleUpdateAsync(payload).ConfigureAwait(false);
                    return JsonSerializer.Serialize(new { ok = true });
                }

                if (string.Equals(payload.Rest, MessagingAction.Delete, StringComparison.OrdinalIgnoreCase))
                {
                    await HandleDeleteAsync(payload).ConfigureAwait(false);
                    return JsonSerializer.Serialize(new { ok = true });
                }

                return CreateErrorResponseJson("unsupported_operation", $"Unsupported rest operation: {payload.Rest}");
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[EXT->APP] Processing error: {ex.Message}");
                return CreateErrorResponseJson("processing_error", ex.Message);
            }
        }

        public static string CreateErrorResponseJson(string code, string message)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                error = new
                {
                    code,
                    message
                }
            });
        }

        private async Task HandleCreateAsync(BrowserExtensionPayloadDto payload)
        {
            var (registration, dek) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string activeDeviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            if (dek.Length != 32)
            {
                throw new InvalidOperationException($"Invalid DEK size: {dek.Length} bytes. Expected 32.");
            }

            string registryJson = JsonSerializer.Serialize(payload.Registry);
            var encryptedRegistry = EncryptRegistryWithAesGcm(dek, registryJson);

            var connectionPayload = new ConnectionRequestDto
            {
                AccountId = accountId,
                DeviceId = activeDeviceId,
                Iv = encryptedRegistry.Iv,
                AuthTag = encryptedRegistry.AuthTag,
                Registry = encryptedRegistry.Ciphertext,
                Label = payload.Label
            };

            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(
                activeDeviceId,
                JsonSerializer.Serialize(connectionPayload),
                "create request");

            await _httpApiClient.PostJsonAsync(ConnectionEndpoint, connectionPayload, headers).ConfigureAwait(false);
        }

        private async Task HandleUpdateAsync(BrowserExtensionPayloadDto payload)
        {
            var (registration, dek) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string activeDeviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            if (dek.Length != 32)
            {
                throw new InvalidOperationException($"Invalid DEK size: {dek.Length} bytes. Expected 32.");
            }

            string registryJson = JsonSerializer.Serialize(payload.Registry);
            var encryptedRegistry = EncryptRegistryWithAesGcm(dek, registryJson);

            var connectionPayload = new ConnectionRequestDto
            {
                Id = payload.Id,
                AccountId = accountId,
                DeviceId = activeDeviceId,
                Iv = encryptedRegistry.Iv,
                AuthTag = encryptedRegistry.AuthTag,
                Registry = encryptedRegistry.Ciphertext,
                Label = payload.Label
            };

            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(
                activeDeviceId,
                JsonSerializer.Serialize(connectionPayload),
                "update request");

            await _httpApiClient.PutJsonAsync(ConnectionEndpoint, connectionPayload, headers).ConfigureAwait(false);
        }

        private async Task HandleDeleteAsync(BrowserExtensionPayloadDto payload)
        {
            if (!payload.Id.HasValue)
            {
                throw new InvalidOperationException("Delete operation requires id.");
            }

            var (registration, _) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string activeDeviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            var deletePayload = new
            {
                id = payload.Id.Value,
                accountId,
                deviceId = activeDeviceId
            };

            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(
                activeDeviceId,
                JsonSerializer.Serialize(deletePayload),
                "delete request");

            await _httpApiClient.DeleteJsonAsync(ConnectionEndpoint, deletePayload, headers).ConfigureAwait(false);
        }

        internal static IReadOnlyDictionary<string, string> CreateSignatureHeaders(
            string deviceId,
            string payloadJson,
            string operationName)
        {
            string keyContainerName = "KeyContainer_" + deviceId;
            string signature = new Pki().Sign(keyContainerName, payloadJson);

            if (string.IsNullOrWhiteSpace(signature))
            {
                throw new InvalidOperationException($"Failed to create signature for {operationName}.");
            }

            return new Dictionary<string, string>
            {
                ["x-signature"] = signature
            };
        }

        private async Task<string> HandleReadAsync()
        {
            var (registration, dek) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string activeDeviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            if (dek.Length != 32)
            {
                throw new InvalidOperationException($"Invalid DEK size: {dek.Length} bytes. Expected 32.");
            }

            var readPayload = new ConnectionReadRequestDto
            {
                AccountId = accountId,
                DeviceId = activeDeviceId
            };

            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(
                activeDeviceId,
                JsonSerializer.Serialize(readPayload),
                "read request");

            List<ConnectionReadItemDto>? items = await _httpApiClient
                .PostJsonAsync<ConnectionReadRequestDto, List<ConnectionReadItemDto>>(ConnectionReadEndpoint, readPayload, headers)
                .ConfigureAwait(false);

            items ??= new List<ConnectionReadItemDto>();

            var results = new List<ConnectionReadResultDto>(items.Count);

            foreach (ConnectionReadItemDto item in items)
            {
                string decryptedRegistry = DecryptRegistryWithAesGcm(dek, item.Iv, item.AuthTag, item.Registry);

                results.Add(new ConnectionReadResultDto
                {
                    Id = item.Id,
                    Label = item.Label,
                    Registry = decryptedRegistry
                });
            }

            _log?.Invoke($"[EXT->APP] Read completed. Returned {results.Count} item(s).");
            return JsonSerializer.Serialize(results);
        }

        private static (string Iv, string AuthTag, string Ciphertext) EncryptRegistryWithAesGcm(byte[] dek, string registryJson)
        {
            byte[] iv = RandomNumberGenerator.GetBytes(12);
            byte[] plaintext = Encoding.UTF8.GetBytes(registryJson);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] authTag = new byte[16];

            using var aes = new AesGcm(dek, 16);
            aes.Encrypt(iv, plaintext, ciphertext, authTag);

            return (
                Convert.ToHexString(iv).ToLowerInvariant(),
                Convert.ToHexString(authTag).ToLowerInvariant(),
                Convert.ToBase64String(ciphertext));
        }

        private static string DecryptRegistryWithAesGcm(byte[] dek, string ivHex, string authTagHex, string cipherBase64)
        {
            byte[] iv = Convert.FromHexString(ivHex);
            byte[] authTag = Convert.FromHexString(authTagHex);
            byte[] ciphertext = Convert.FromBase64String(cipherBase64);
            byte[] plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(dek, 16);
            aes.Decrypt(iv, ciphertext, authTag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
    }
}
