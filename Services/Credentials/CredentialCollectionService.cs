using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class CredentialCollectionService
    {
        private const string TrayDeviceName = "ZeroIntrusion Tray";
        private readonly RegisteredDeviceService _registeredDeviceService;
        private readonly HttpApiClient _httpApiClient;
        private readonly AuthenticationService _authenticationService;
        private readonly ExtensionSessionStateStore _extensionSessionStateStore;
        private readonly Action<string>? _log;

        public CredentialCollectionService(
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

        public async Task<IReadOnlyList<CredentialGroupDto>> ReadGroupsAsync()
        {
            DesktopRegistration registration = _registeredDeviceService.ReadRegistration();
            string accountId = AppEnvironment.GetActiveNamespaceAccountId();
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceId();

            var payload = new ConnectionReadRequestDto
            {
                AccountId = accountId,
                DesktopDeviceId = desktopDeviceId,
                DeviceName = TrayDeviceName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(payload);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(desktopDeviceId, payloadJson, "tray group read request");

            List<CredentialGroupDto>? groups = await _httpApiClient
                .PostJsonAsync<ConnectionReadRequestDto, List<CredentialGroupDto>>(CredentialApiEndpoints.GroupRead, payload, headers)
                .ConfigureAwait(false);

            if (groups == null)
            {
                return Array.Empty<CredentialGroupDto>();
            }

            return groups;
        }

        public async Task<IReadOnlyList<CredentialEntryListItem>> ReadEntriesAsync()
        {
            DesktopRegistration registration = _registeredDeviceService.ReadRegistration();
            string accountId = AppEnvironment.GetActiveNamespaceAccountId();
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceId();

            var payload = new ConnectionReadRequestDto
            {
                AccountId = accountId,
                DesktopDeviceId = desktopDeviceId,
                DeviceName = TrayDeviceName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(payload);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(desktopDeviceId, payloadJson, "tray credential read request");

            List<ConnectionReadItemDto>? items = await _httpApiClient
                .PostJsonAsync<ConnectionReadRequestDto, List<ConnectionReadItemDto>>(CredentialApiEndpoints.ConnectionRead, payload, headers)
                .ConfigureAwait(false);

            items ??= new List<ConnectionReadItemDto>();

            return items
                .Select(item => new CredentialEntryListItem(
                    item.Id,
                    item.Label,
                    item.Group?.GroupId ?? string.Empty,
                    item.Group?.GroupName ?? string.Empty,
                    item.Iv,
                    item.AuthTag,
                    item.Registry))
                .ToArray();
        }

        public Task<CredentialEntryDetail> ReadEntryDetailAsync(CredentialEntryListItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            var (_, dek) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            BrowserRegistryDto registry = DeserializeRegistry(dek, item.Iv, item.AuthTag, item.EncryptedRegistry);

            return Task.FromResult(new CredentialEntryDetail(
                item.Id,
                item.Label,
                registry,
                item.GroupId,
                item.GroupName));
        }

        public async Task<CredentialEntryListItem?> CreateEntryAsync(CredentialEntryDraft draft)
        {
            await EnsureAuthorizedForCredentialAccessAsync().ConfigureAwait(false);

            var (registration, dek) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountId();
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceId();

            string registryJson = JsonSerializer.Serialize(draft.Registry);
            var encryptedRegistry = EncryptRegistryWithAesGcm(dek, registryJson);

            var payload = new ConnectionRequestDto
            {
                AccountId = accountId,
                DesktopDeviceId = desktopDeviceId,
                DeviceName = TrayDeviceName,
                Iv = encryptedRegistry.Iv,
                AuthTag = encryptedRegistry.AuthTag,
                Registry = encryptedRegistry.Ciphertext,
                Label = draft.Label,
                GroupId = string.IsNullOrWhiteSpace(draft.GroupId) ? null : draft.GroupId,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(payload);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(desktopDeviceId, payloadJson, "tray credential create request");

            ConnectionReadItemDto? response = await _httpApiClient
                .PostJsonAsync<ConnectionRequestDto, ConnectionReadItemDto>(CredentialApiEndpoints.Connection, payload, headers)
                .ConfigureAwait(false);

            return response == null ? null : new CredentialEntryListItem(
                response.Id,
                response.Label,
                response.Group?.GroupId ?? string.Empty,
                response.Group?.GroupName ?? string.Empty,
                response.Iv,
                response.AuthTag,
                response.Registry);
        }

        public async Task<CredentialEntryListItem?> UpdateEntryAsync(CredentialEntryDraft draft)
        {
            if (draft.Id == null)
            {
                throw new InvalidOperationException("Credential update requires numeric id.");
            }

            await EnsureAuthorizedForCredentialAccessAsync().ConfigureAwait(false);

            var (registration, dek) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountId();
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceId();

            string registryJson = JsonSerializer.Serialize(draft.Registry);
            var encryptedRegistry = EncryptRegistryWithAesGcm(dek, registryJson);

            var payload = new ConnectionRequestDto
            {
                Id = draft.Id.Value,
                AccountId = accountId,
                DesktopDeviceId = desktopDeviceId,
                DeviceName = TrayDeviceName,
                Iv = encryptedRegistry.Iv,
                AuthTag = encryptedRegistry.AuthTag,
                Registry = encryptedRegistry.Ciphertext,
                Label = draft.Label,
                GroupId = string.IsNullOrWhiteSpace(draft.GroupId) ? null : draft.GroupId,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(payload);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(desktopDeviceId, payloadJson, "tray credential update request");

            ConnectionReadItemDto? response = await _httpApiClient
                .PutJsonAsync<ConnectionRequestDto, ConnectionReadItemDto>(CredentialApiEndpoints.Connection, payload, headers)
                .ConfigureAwait(false);

            return response == null ? null : new CredentialEntryListItem(
                response.Id,
                response.Label,
                response.Group?.GroupId ?? string.Empty,
                response.Group?.GroupName ?? string.Empty,
                response.Iv,
                response.AuthTag,
                response.Registry);
        }

        public async Task<CredentialGroupDto?> CreateGroupAsync(string groupName, string? groupId = null)
        {
            await EnsureAuthorizedForCredentialAccessAsync().ConfigureAwait(false);

            var (registration, _) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountId();
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceId();

            var request = new CredentialGroupCreateRequestDto
            {
                AccountId = accountId,
                DesktopDeviceId = desktopDeviceId,
                GroupId = string.IsNullOrWhiteSpace(groupId) ? Guid.NewGuid().ToString("D") : groupId,
                GroupName = groupName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(desktopDeviceId, payloadJson, "tray group create request");

            return await _httpApiClient
                .PostJsonAsync<CredentialGroupCreateRequestDto, CredentialGroupDto>(CredentialApiEndpoints.Group, request, headers)
                .ConfigureAwait(false);
        }

        public async Task<CredentialGroupDto?> UpdateGroupAsync(string groupId, string groupName)
        {
            await EnsureAuthorizedForCredentialAccessAsync().ConfigureAwait(false);

            var (registration, _) = _registeredDeviceService.ReadRegistrationAndDecryptDek();
            string accountId = AppEnvironment.GetActiveNamespaceAccountId();
            string desktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceId();

            var request = new CredentialGroupUpdateRequestDto
            {
                AccountId = accountId,
                DesktopDeviceId = desktopDeviceId,
                GroupId = groupId,
                GroupName = groupName,
                Nonce = NonceGenerator.Create()
            };

            string payloadJson = JsonSerializer.Serialize(request);
            IReadOnlyDictionary<string, string> headers = BrowserCommandHandler.CreateSignatureHeaders(desktopDeviceId, payloadJson, "tray group update request");

            return await _httpApiClient
                .PutJsonAsync<CredentialGroupUpdateRequestDto, CredentialGroupDto>(CredentialApiEndpoints.Group, request, headers)
                .ConfigureAwait(false);
        }

        private static BrowserRegistryDto DeserializeRegistry(byte[] dek, string iv, string authTag, string encryptedRegistry)
        {
            string registryJson = DecryptRegistryWithAesGcm(dek, iv, authTag, encryptedRegistry);
            return JsonSerializer.Deserialize<BrowserRegistryDto>(registryJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new BrowserRegistryDto();
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

        private async Task EnsureAuthorizedForCredentialAccessAsync()
        {
            bool authorized = await _authenticationService.EnsureAuthorizedAsync(allowInteractiveLogin: false).ConfigureAwait(false);
            if (!authorized)
            {
                _extensionSessionStateStore.MarkInvalid("authorization_declined");
                throw new InvalidOperationException("No active desktop attestation. Log in again from the tray app.");
            }

            bool hasValidCredential = await _authenticationService.HasValidCredentialForActiveNamespaceAsync().ConfigureAwait(false);
            if (!hasValidCredential)
            {
                _extensionSessionStateStore.MarkInvalid("active_namespace_credential_missing");
                throw new InvalidOperationException("The selected namespace does not have an active backend credential for this user.");
            }

            _extensionSessionStateStore.MarkAuthorized();
            _log?.Invoke("[TRAY] Credential collection access authorized.");
        }
    }

    internal sealed record CredentialEntryListItem(
        int Id,
        string Label,
        string GroupId,
        string GroupName,
        string Iv,
        string AuthTag,
        string EncryptedRegistry);

    internal sealed record CredentialEntryDetail(
        int Id,
        string Label,
        BrowserRegistryDto Registry,
        string GroupId,
        string GroupName);

    internal sealed record CredentialEntryDraft(
        int? Id,
        string Label,
        BrowserRegistryDto Registry,
        string GroupId);
}