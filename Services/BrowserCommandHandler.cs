using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class BrowserCommandHandler : INativeMessageProcessor
    {
        private const string TrayDeviceName = "ZeroIntrusion Tray";
        private static string ConnectionEndpoint => AppEnvironment.BuildApiUrl(ApiRoutes.CredentialStore);
        private static string ConnectionReadEndpoint => AppEnvironment.BuildApiUrl(ApiRoutes.CredentialStoreAll);
        private static string GroupEndpoint => AppEnvironment.BuildApiUrl(ApiRoutes.CredentialStoreGroup);
        private static string GroupReadEndpoint => AppEnvironment.BuildApiUrl(ApiRoutes.CredentialStoreGroupAll);
        
        private readonly RegisteredDeviceService _registeredDeviceService;
        private readonly HttpApiClient _httpApiClient;
        private readonly AuthenticationService _authenticationService;
        private readonly ExtensionSessionStateStore _extensionSessionStateStore;

        private readonly Action<string>? _log;        

        public BrowserCommandHandler(
            RegisteredDeviceService registeredDeviceService,
            HttpApiClient httpApiClient,
            AuthenticationService authenticationService,
            ExtensionSessionStateStore extensionSessionStateStore,
            Action<string>? log = null)
        {
            _registeredDeviceService = registeredDeviceService;
            _httpApiClient = httpApiClient;
            _authenticationService = authenticationService;
            _extensionSessionStateStore = extensionSessionStateStore;
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
                if (IsSessionStateRequest(payload))
                {
                    return JsonSerializer.Serialize(_extensionSessionStateStore.GetSnapshot());
                }

                bool authorized = await _authenticationService.EnsureAuthorizedAsync(allowInteractiveLogin: false).ConfigureAwait(false);
                if (!authorized)
                {
                    _extensionSessionStateStore.MarkInvalid("authorization_declined");
                    return CreateErrorResponseJson("authorization_declined", "No active desktop attestation. Log in again from the tray app.");
                }

                bool hasValidCredential = await _authenticationService.HasValidCredentialForActiveNamespaceAsync().ConfigureAwait(false);
                if (!hasValidCredential)
                {
                    _extensionSessionStateStore.MarkInvalid("active_namespace_credential_missing");
                    return CreateErrorResponseJson("authorization_declined", "The selected namespace does not have an active backend credential for this user.");
                }

                _extensionSessionStateStore.MarkAuthorized();

                if (IsGroupRequest(payload))
                {
                    return await ProcessGroupRequestAsync(payload).ConfigureAwait(false);
                }

                if (string.Equals(payload.Rest, MessagingAction.Read, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleReadAsync().ConfigureAwait(false);
                }

                if (string.Equals(payload.Rest, MessagingAction.Create, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleCreateAsync(payload).ConfigureAwait(false);
                }

                if (string.Equals(payload.Rest, MessagingAction.Update, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleUpdateAsync(payload).ConfigureAwait(false);
                }

                if (string.Equals(payload.Rest, MessagingAction.Delete, StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleDeleteAsync(payload).ConfigureAwait(false);
                }

                return CreateErrorResponseJson("unsupported_operation", $"Unsupported rest operation: {payload.Rest}");
            }
            catch (ApiRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _authenticationService.RevokeAuthorization();
                _extensionSessionStateStore.MarkInvalid("authorization_revoked");
                _log?.Invoke($"[EXT->APP] Credential-store request lost attestation: {ex.Message}");
                return CreateErrorResponseJson("authorization_expired", "Desktop attestation expired or was revoked. Log in again from the tray app.");
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

        private async Task<string> HandleCreateAsync(BrowserExtensionPayloadDto payload)
        {
            var (registration, dek) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string deviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            if (dek.Length != 32)
            {
                throw new InvalidOperationException($"Invalid DEK size: {dek.Length} bytes. Expected 32.");
            }

            string registryJson = JsonSerializer.Serialize(payload.Registry);
            var encryptedRegistry = EncryptRegistryWithAesGcm(dek, registryJson);

            var connectionPayload = new ConnectionRequestDto
            {
                AccountId = accountId,
                DeviceId = deviceId,
                DeviceName = TrayDeviceName,
                Iv = encryptedRegistry.Iv,
                AuthTag = encryptedRegistry.AuthTag,
                Registry = encryptedRegistry.Ciphertext,
                Label = payload.Label,
                GroupId = string.IsNullOrWhiteSpace(payload.GroupId) ? null : payload.GroupId,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(connectionPayload);

            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(
                deviceId,
                payloadJson,
                "create request");

            ConnectionReadItemDto? response = await _httpApiClient
                .PostJsonAsync<ConnectionRequestDto, ConnectionReadItemDto>(ConnectionEndpoint, connectionPayload, headers)
                .ConfigureAwait(false);

            return response == null
                ? JsonSerializer.Serialize(new { ok = true })
                : SerializeCredentialResult(dek, response);
        }

        private async Task<string> HandleUpdateAsync(BrowserExtensionPayloadDto payload)
        {
            if (!TryGetNumericId(payload, out int credentialId))
            {
                throw new InvalidOperationException("Credential update requires numeric id.");
            }

            var (registration, dek) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string deviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            if (dek.Length != 32)
            {
                throw new InvalidOperationException($"Invalid DEK size: {dek.Length} bytes. Expected 32.");
            }

            string registryJson = JsonSerializer.Serialize(payload.Registry);
            var encryptedRegistry = EncryptRegistryWithAesGcm(dek, registryJson);

            var connectionPayload = new ConnectionRequestDto
            {
                Id = credentialId,
                AccountId = accountId,
                DeviceId = deviceId,
                DeviceName = TrayDeviceName,
                Iv = encryptedRegistry.Iv,
                AuthTag = encryptedRegistry.AuthTag,
                Registry = encryptedRegistry.Ciphertext,
                Label = payload.Label,
                GroupId = string.IsNullOrWhiteSpace(payload.GroupId) ? null : payload.GroupId,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(connectionPayload);

            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(
                deviceId,
                payloadJson,
                "update request");

            ConnectionReadItemDto? response = await _httpApiClient
                .PutJsonAsync<ConnectionRequestDto, ConnectionReadItemDto>(ConnectionEndpoint, connectionPayload, headers)
                .ConfigureAwait(false);

            return response == null
                ? JsonSerializer.Serialize(new { ok = true })
                : SerializeCredentialResult(dek, response);
        }

        private async Task<string> HandleDeleteAsync(BrowserExtensionPayloadDto payload)
        {
            if (!TryGetNumericId(payload, out int credentialId))
            {
                throw new InvalidOperationException("Credential delete requires numeric id.");
            }

            var (registration, dek) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string deviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            var deletePayload = new
            {
                id = credentialId,
                accountId,
                deviceId,
                deviceName = TrayDeviceName,
                nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(deletePayload);

            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(
                deviceId,
                payloadJson,
                "delete request");

            ConnectionReadItemDto? response = await _httpApiClient
                .DeleteJsonAsync<object, ConnectionReadItemDto>(ConnectionEndpoint, deletePayload, headers)
                .ConfigureAwait(false);

            return response == null
                ? JsonSerializer.Serialize(new { ok = true })
                : SerializeCredentialResult(dek, response);
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
            string deviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            if (dek.Length != 32)
            {
                throw new InvalidOperationException($"Invalid DEK size: {dek.Length} bytes. Expected 32.");
            }

            var readPayload = new ConnectionReadRequestDto
            {
                AccountId = accountId,
                DeviceId = deviceId,
                DeviceName = TrayDeviceName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(readPayload);

            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(
                deviceId,
                payloadJson,
                "read request");

            List<ConnectionReadItemDto>? items = await _httpApiClient
                .PostJsonAsync<ConnectionReadRequestDto, List<ConnectionReadItemDto>>(ConnectionReadEndpoint, readPayload, headers)
                .ConfigureAwait(false);

            items ??= new List<ConnectionReadItemDto>();

            var results = new List<ConnectionReadResultDto>(items.Count);

            foreach (ConnectionReadItemDto item in items)
            {
                results.Add(BuildCredentialResult(dek, item));
            }

            _log?.Invoke($"[EXT->APP] Read completed. Returned {results.Count} item(s).");
            return JsonSerializer.Serialize(results);
        }

        private async Task<string> ProcessGroupRequestAsync(BrowserExtensionPayloadDto payload)
        {
            if (string.Equals(payload.Rest, MessagingAction.Read, StringComparison.OrdinalIgnoreCase))
            {
                return await HandleGroupReadAsync().ConfigureAwait(false);
            }

            if (string.Equals(payload.Rest, MessagingAction.Create, StringComparison.OrdinalIgnoreCase))
            {
                return await HandleGroupCreateAsync(payload).ConfigureAwait(false);
            }

            if (string.Equals(payload.Rest, MessagingAction.Update, StringComparison.OrdinalIgnoreCase))
            {
                return await HandleGroupUpdateAsync(payload).ConfigureAwait(false);
            }

            if (string.Equals(payload.Rest, MessagingAction.Delete, StringComparison.OrdinalIgnoreCase))
            {
                return await HandleGroupDeleteAsync(payload).ConfigureAwait(false);
            }

            return CreateErrorResponseJson("unsupported_operation", $"Unsupported group rest operation: {payload.Rest}");
        }

        private async Task<string> HandleGroupReadAsync()
        {
            var (registration, _) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string deviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            var payload = new ConnectionReadRequestDto
            {
                AccountId = accountId,
                DeviceId = deviceId,
                DeviceName = TrayDeviceName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(payload);
            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(deviceId, payloadJson, "group read request");

            List<CredentialGroupDto>? groups = await _httpApiClient
                .PostJsonAsync<ConnectionReadRequestDto, List<CredentialGroupDto>>(GroupReadEndpoint, payload, headers)
                .ConfigureAwait(false);

            groups ??= new List<CredentialGroupDto>();
            return JsonSerializer.Serialize(groups);
        }

        private async Task<string> HandleGroupCreateAsync(BrowserExtensionPayloadDto payload)
        {
            if (string.IsNullOrWhiteSpace(payload.GroupName))
            {
                throw new InvalidOperationException("Group create requires groupName.");
            }

            var (registration, _) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string deviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            var request = new CredentialGroupCreateRequestDto
            {
                AccountId = accountId,
                DeviceId = deviceId,
                GroupId = string.IsNullOrWhiteSpace(payload.GroupId) ? Guid.NewGuid().ToString("D") : payload.GroupId,
                GroupName = payload.GroupName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(deviceId, payloadJson, "group create request");

            CredentialGroupDto? response = await _httpApiClient
                .PostJsonAsync<CredentialGroupCreateRequestDto, CredentialGroupDto>(GroupEndpoint, request, headers)
                .ConfigureAwait(false);

            return response == null
                ? JsonSerializer.Serialize(new { ok = true, groupId = request.GroupId })
                : JsonSerializer.Serialize(response);
        }

        private async Task<string> HandleGroupUpdateAsync(BrowserExtensionPayloadDto payload)
        {
            if (string.IsNullOrWhiteSpace(payload.GroupId))
            {
                throw new InvalidOperationException("Group update requires groupId.");
            }

            if (string.IsNullOrWhiteSpace(payload.GroupName))
            {
                throw new InvalidOperationException("Group update requires a non-empty groupName.");
            }

            var (registration, _) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string deviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            var request = new CredentialGroupUpdateRequestDto
            {
                AccountId = accountId,
                DeviceId = deviceId,
                GroupId = payload.GroupId,
                GroupName = payload.GroupName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(deviceId, payloadJson, "group update request");

            CredentialGroupDto? response = await _httpApiClient
                .PutJsonAsync<CredentialGroupUpdateRequestDto, CredentialGroupDto>(GroupEndpoint, request, headers)
                .ConfigureAwait(false);

            return response == null
                ? JsonSerializer.Serialize(new { ok = true, groupId = request.GroupId })
                : JsonSerializer.Serialize(response);
        }

        private async Task<string> HandleGroupDeleteAsync(BrowserExtensionPayloadDto payload)
        {
            if (string.IsNullOrWhiteSpace(payload.GroupId))
            {
                throw new InvalidOperationException("Group delete requires groupId.");
            }

            var (registration, _) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string deviceId = AppEnvironment.GetActiveNamespaceDeviceIdOrDefault(registration.DeviceId);

            var request = new CredentialGroupDeleteRequestDto
            {
                AccountId = accountId,
                DeviceId = deviceId,
                GroupId = payload.GroupId,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(deviceId, payloadJson, "group delete request");

            CredentialGroupDto? response = await _httpApiClient
                .DeleteJsonAsync<CredentialGroupDeleteRequestDto, CredentialGroupDto>(GroupEndpoint, request, headers)
                .ConfigureAwait(false);

            return response == null
                ? JsonSerializer.Serialize(new { ok = true, groupId = request.GroupId })
                : JsonSerializer.Serialize(response);
        }

        private static bool IsGroupRequest(BrowserExtensionPayloadDto payload)
        {
            return string.Equals(payload.Resource, MessagingResource.Group, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSessionStateRequest(BrowserExtensionPayloadDto payload)
        {
            return string.Equals(payload.Rest, MessagingAction.State, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(payload.Resource, MessagingResource.Session, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetNumericId(BrowserExtensionPayloadDto payload, out int id)
        {
            id = 0;

            if (!payload.Id.HasValue)
            {
                return false;
            }

            JsonElement value = payload.Id.Value;

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.TryGetInt32(out id);
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return int.TryParse(value.GetString(), out id);
            }

            return false;
        }

        private static bool TryGetStringId(BrowserExtensionPayloadDto payload, out string id)
        {
            id = string.Empty;

            if (!payload.Id.HasValue)
            {
                return false;
            }

            JsonElement value = payload.Id.Value;

            if (value.ValueKind == JsonValueKind.String)
            {
                id = value.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(id);
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                id = value.GetRawText();
                return !string.IsNullOrWhiteSpace(id);
            }

            return false;
        }

        private static string SerializeCredentialResult(byte[] dek, ConnectionReadItemDto item)
        {
            return JsonSerializer.Serialize(BuildCredentialResult(dek, item));
        }

        private static ConnectionReadResultDto BuildCredentialResult(byte[] dek, ConnectionReadItemDto item)
        {
            string decryptedRegistry = DecryptRegistryWithAesGcm(dek, item.Iv, item.AuthTag, item.Registry);

            return new ConnectionReadResultDto
            {
                Id = item.Id,
                Label = item.Label,
                CredentialGroupId = item.CredentialGroupId,
                Registry = decryptedRegistry,
                Group = item.Group
            };
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
