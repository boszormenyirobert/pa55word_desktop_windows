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
        private readonly RegisteredDeviceService _registeredDeviceService;
        private readonly HttpApiClient _httpApiClient;
        private readonly AuthenticationService _authenticationService;
        private readonly ExtensionSessionStateStore _extensionSessionStateStore;
        private readonly object _credentialReadCacheSync = new();
        private readonly Dictionary<int, ConnectionReadItemDto> _credentialReadCache = new();
        private string? _credentialReadCacheContextKey;

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
                    _log?.Invoke("[EXT->APP] Session state request received.");
                    return JsonSerializer.Serialize(_extensionSessionStateStore.GetSnapshot());
                }

                _log?.Invoke($"[EXT->APP] Request received. rest='{payload.Rest}', resource='{payload.Resource ?? MessagingResource.Credential}', id='{payload.Id?.GetRawText() ?? "<null>"}'.");

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

                if (IsCredentialDetailRequest(payload))
                {
                    return await HandleReadDetailAsync(payload).ConfigureAwait(false);
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
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceIdOrDefault(registration.DesktopDeviceId);

            if (dek.Length != 32)
            {
                throw new InvalidOperationException($"Invalid DEK size: {dek.Length} bytes. Expected 32.");
            }

            string registryJson = JsonSerializer.Serialize(payload.Registry);
            var encryptedRegistry = EncryptRegistryWithAesGcm(dek, registryJson);

            var connectionPayload = new ConnectionRequestDto
            {
                AccountId = accountId,
                DesktopDeviceId = desktopDeviceId,
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
                desktopDeviceId,
                payloadJson,
                "create request");

            ConnectionReadItemDto? response = await _httpApiClient
                .PostJsonAsync<ConnectionRequestDto, ConnectionReadItemDto>(CredentialApiEndpoints.Connection, connectionPayload, headers)
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
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceIdOrDefault(registration.DesktopDeviceId);

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
                DesktopDeviceId = desktopDeviceId,
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
                desktopDeviceId,
                payloadJson,
                "update request");

            ConnectionReadItemDto? response = await _httpApiClient
                .PutJsonAsync<ConnectionRequestDto, ConnectionReadItemDto>(CredentialApiEndpoints.Connection, connectionPayload, headers)
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
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceIdOrDefault(registration.DesktopDeviceId);

            var deletePayload = new
            {
                id = credentialId,
                accountId,
                desktopDeviceId,
                deviceName = TrayDeviceName,
                nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(deletePayload);

            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(
                desktopDeviceId,
                payloadJson,
                "delete request");

            ConnectionReadItemDto? response = await _httpApiClient
                .DeleteJsonAsync<object, ConnectionReadItemDto>(CredentialApiEndpoints.Connection, deletePayload, headers)
                .ConfigureAwait(false);

            return response == null
                ? JsonSerializer.Serialize(new { ok = true })
                : SerializeCredentialResult(dek, response);
        }

        internal static IReadOnlyDictionary<string, string> CreateSignatureHeaders(
            string desktopDeviceId,
            string payloadJson,
            string operationName)
        {
            return RequestSigner.CreateSignatureHeaders(desktopDeviceId, payloadJson, operationName);
        }

        private async Task<string> HandleReadAsync()
        {
            (List<ConnectionReadItemDto> items, string contextKey) = await ReadCredentialItemsAsync().ConfigureAwait(false);
            ReplaceCredentialReadCache(items, contextKey);
            _log?.Invoke($"[EXT->APP] Credential read fetched {items.Count} item(s) from backend.");

            var results = new List<ConnectionReadResultDto>(items.Count);

            foreach (ConnectionReadItemDto item in items)
            {
                results.Add(BuildCredentialListResult(item));
            }

            string responseJson = JsonSerializer.Serialize(results);
            _log?.Invoke($"[EXT->APP] Read completed. Returned {results.Count} item(s), responseChars={responseJson.Length}.");
            return responseJson;
        }

        private Task<string> HandleReadDetailAsync(BrowserExtensionPayloadDto payload)
        {
            if (!TryGetNumericId(payload, out int credentialId))
            {
                throw new InvalidOperationException("Credential detail read requires numeric id.");
            }

            CredentialReadContext context = GetCredentialReadContext();
            var (_, dek) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            if (dek.Length != 32)
            {
                throw new InvalidOperationException($"Invalid DEK size: {dek.Length} bytes. Expected 32.");
            }

            ConnectionReadItemDto item = GetCachedCredentialReadItem(credentialId, context.ContextKey)
                ?? throw new InvalidOperationException("Credential detail cache miss or stale context. Reload the credential list and try again.");

            _log?.Invoke($"[EXT->APP] Credential detail read using cached item. id={credentialId}, label='{item.Label}'.");

            return Task.FromResult(SerializeCredentialResult(dek, item));
        }

        private async Task<(List<ConnectionReadItemDto> Items, string ContextKey)> ReadCredentialItemsAsync()
        {
            CredentialReadContext context = GetCredentialReadContext();

            var readPayload = new ConnectionReadRequestDto
            {
                AccountId = context.AccountId,
                DesktopDeviceId = context.DesktopDeviceId,
                DeviceName = TrayDeviceName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(readPayload);

            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(
                context.DesktopDeviceId,
                payloadJson,
                "read request");

            List<ConnectionReadItemDto>? items = await _httpApiClient
                .PostJsonAsync<ConnectionReadRequestDto, List<ConnectionReadItemDto>>(CredentialApiEndpoints.ConnectionRead, readPayload, headers)
                .ConfigureAwait(false);

            return (items ?? new List<ConnectionReadItemDto>(), context.ContextKey);
        }

        private void ReplaceCredentialReadCache(IReadOnlyList<ConnectionReadItemDto> items, string contextKey)
        {
            lock (_credentialReadCacheSync)
            {
                _credentialReadCache.Clear();
                _credentialReadCacheContextKey = contextKey;

                foreach (ConnectionReadItemDto item in items)
                {
                    _credentialReadCache[item.Id] = item;
                }
            }
        }

        private ConnectionReadItemDto? GetCachedCredentialReadItem(int credentialId, string contextKey)
        {
            lock (_credentialReadCacheSync)
            {
                if (!string.Equals(_credentialReadCacheContextKey, contextKey, StringComparison.Ordinal))
                {
                    _credentialReadCache.Clear();
                    _credentialReadCacheContextKey = null;
                    return null;
                }

                return _credentialReadCache.TryGetValue(credentialId, out ConnectionReadItemDto? item)
                    ? item
                    : null;
            }
        }

        private CredentialReadContext GetCredentialReadContext()
        {
            DesktopRegistration registration = _registeredDeviceService.ReadRegistration();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceIdOrDefault(registration.DesktopDeviceId);
            string namespaceId = AppEnvironment.GetActiveNamespaceId() ?? string.Empty;
            string contextKey = $"{registration.UserKey}|{namespaceId}|{accountId}|{desktopDeviceId}";

            return new CredentialReadContext(accountId, desktopDeviceId, contextKey);
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
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceIdOrDefault(registration.DesktopDeviceId);

            var payload = new ConnectionReadRequestDto
            {
                AccountId = accountId,
                DesktopDeviceId = desktopDeviceId,
                DeviceName = TrayDeviceName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(payload);
            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(desktopDeviceId, payloadJson, "group read request");

            List<CredentialGroupDto>? groups = await _httpApiClient
                .PostJsonAsync<ConnectionReadRequestDto, List<CredentialGroupDto>>(CredentialApiEndpoints.GroupRead, payload, headers)
                .ConfigureAwait(false);

            groups ??= new List<CredentialGroupDto>();
            string responseJson = JsonSerializer.Serialize(groups);
            _log?.Invoke($"[EXT->APP] Group read completed. Returned {groups.Count} group(s), responseChars={responseJson.Length}.");
            return responseJson;
        }

        private async Task<string> HandleGroupCreateAsync(BrowserExtensionPayloadDto payload)
        {
            if (string.IsNullOrWhiteSpace(payload.GroupName))
            {
                throw new InvalidOperationException("Group create requires groupName.");
            }

            var (registration, _) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountIdOrDefault(registration.AccountId);
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceIdOrDefault(registration.DesktopDeviceId);

            var request = new CredentialGroupCreateRequestDto
            {
                AccountId = accountId,
                DesktopDeviceId = desktopDeviceId,
                GroupId = string.IsNullOrWhiteSpace(payload.GroupId) ? Guid.NewGuid().ToString("D") : payload.GroupId,
                GroupName = payload.GroupName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(desktopDeviceId, payloadJson, "group create request");

            CredentialGroupDto? response = await _httpApiClient
                .PostJsonAsync<CredentialGroupCreateRequestDto, CredentialGroupDto>(CredentialApiEndpoints.Group, request, headers)
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
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceIdOrDefault(registration.DesktopDeviceId);

            var request = new CredentialGroupUpdateRequestDto
            {
                AccountId = accountId,
                DesktopDeviceId = desktopDeviceId,
                GroupId = payload.GroupId,
                GroupName = payload.GroupName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(desktopDeviceId, payloadJson, "group update request");

            CredentialGroupDto? response = await _httpApiClient
                .PutJsonAsync<CredentialGroupUpdateRequestDto, CredentialGroupDto>(CredentialApiEndpoints.Group, request, headers)
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
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceIdOrDefault(registration.DesktopDeviceId);

            var request = new CredentialGroupDeleteRequestDto
            {
                AccountId = accountId,
                DesktopDeviceId = desktopDeviceId,
                GroupId = payload.GroupId,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = CreateSignatureHeaders(desktopDeviceId, payloadJson, "group delete request");

            CredentialGroupDto? response = await _httpApiClient
                .DeleteJsonAsync<CredentialGroupDeleteRequestDto, CredentialGroupDto>(CredentialApiEndpoints.Group, request, headers)
                .ConfigureAwait(false);

            return response == null
                ? JsonSerializer.Serialize(new { ok = true, groupId = request.GroupId })
                : JsonSerializer.Serialize(response);
        }

        private static bool IsGroupRequest(BrowserExtensionPayloadDto payload)
        {
            return string.Equals(payload.Resource, MessagingResource.Group, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCredentialDetailRequest(BrowserExtensionPayloadDto payload)
        {
            return string.Equals(payload.Rest, MessagingAction.Read, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(payload.Resource, MessagingResource.CredentialDetail, StringComparison.OrdinalIgnoreCase);
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
                Iv = item.Iv,
                AuthTag = item.AuthTag,
                Registry = decryptedRegistry,
                Group = item.Group
            };
        }

        private static ConnectionReadResultDto BuildCredentialListResult(ConnectionReadItemDto item)
        {
            return new ConnectionReadResultDto
            {
                Id = item.Id,
                Label = item.Label,
                CredentialGroupId = item.CredentialGroupId,
                Iv = string.Empty,
                AuthTag = string.Empty,
                Registry = string.Empty,
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

        private sealed record CredentialReadContext(
            string AccountId,
            string DesktopDeviceId,
            string ContextKey);
    }
}
