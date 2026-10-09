using System.Diagnostics;
using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed partial class RegistrationConfigStore
    {
        private readonly string filePath = AppPaths.GetSourceRootFilePath("config.json");

        private static readonly JsonSerializerOptions ConfigJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public bool ContainsConfiguredUrl(string url, string? userKey = null)
        {
            string normalizedUrl = NormalizeUrlForComparison(url);

            if (TryReadRegistrations(out List<DesktopRegistration>? registrations) && registrations != null)
            {
                IEnumerable<DesktopRegistration> registrationsToInspect = string.IsNullOrWhiteSpace(userKey)
                    ? registrations
                    : registrations.Where(registration =>
                        string.Equals(registration.UserKey, userKey, StringComparison.Ordinal));

                return registrationsToInspect.Any(registration => registration.Namespaces.Any(namespaceItem =>
                    string.Equals(
                        NormalizeUrlForComparison(namespaceItem.Url),
                        normalizedUrl,
                        StringComparison.OrdinalIgnoreCase)));
            }

            ConfigModel bootstrapConfig = LoadBootstrapConfig();
            return !string.IsNullOrWhiteSpace(bootstrapConfig.BaseUrl) &&
                string.Equals(
                    NormalizeUrlForComparison(bootstrapConfig.BaseUrl),
                    normalizedUrl,
                    StringComparison.OrdinalIgnoreCase);
        }

        public bool TryReadRegistration(string? userId, out DesktopRegistration? registration)
        {
            registration = null;

            if (!TryReadRegistrations(out List<DesktopRegistration>? registrations) || registrations is not { Count: > 0 })
            {
                return false;
            }

            DesktopRegistration? parsedRegistration = SelectRegistration(registrations, userId);

            if (!IsValidRegistration(parsedRegistration))
            {
                return false;
            }

            registration = parsedRegistration;
            return true;
        }

        public DesktopRegistration ReadRegistration(string? userId = null)
        {
            if (!TryReadRegistration(userId, out DesktopRegistration? registration))
            {
                throw new InvalidDataException("config.json does not contain a valid namespace registration with accountId/desktopDeviceId/encDek.");
            }

            return registration!;
        }

        public void SaveRegistration(DesktopRegistration registration)
        {
            registration.UserId = EnsureUserId(registration);

            NamespaceItemDto? primaryNamespace = registration.Namespaces.FirstOrDefault();
            string primaryDesktopDeviceId = !string.IsNullOrWhiteSpace(primaryNamespace?.DesktopDeviceId)
                ? primaryNamespace.DesktopDeviceId
                : registration.DesktopDeviceId;
            string primaryEncDek = !string.IsNullOrWhiteSpace(primaryNamespace?.EncDek)
                ? primaryNamespace.EncDek
                : registration.EncDek;
            string primaryAccountId = !string.IsNullOrWhiteSpace(primaryNamespace?.AccountId)
                ? primaryNamespace.AccountId
                : registration.AccountId;

            foreach (NamespaceItemDto namespaceItem in registration.Namespaces)
            {
                if (string.IsNullOrWhiteSpace(namespaceItem.AccountId) && !string.IsNullOrWhiteSpace(primaryAccountId))
                {
                    namespaceItem.AccountId = primaryAccountId;
                }

                if (string.IsNullOrWhiteSpace(namespaceItem.DesktopDeviceId))
                {
                    namespaceItem.DesktopDeviceId = primaryDesktopDeviceId;
                }

                if (string.IsNullOrWhiteSpace(namespaceItem.EncDek) &&
                    string.Equals(namespaceItem.DesktopDeviceId, primaryDesktopDeviceId, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(primaryEncDek))
                {
                    namespaceItem.EncDek = primaryEncDek;
                }

                if (string.IsNullOrWhiteSpace(namespaceItem.DeviceName) &&
                    string.Equals(namespaceItem.DesktopDeviceId, primaryDesktopDeviceId, StringComparison.Ordinal))
                {
                    namespaceItem.DeviceName = Environment.MachineName;
                }
            }

            List<DesktopRegistration> registrations = TryReadRegistrations(out List<DesktopRegistration>? existingRegistrations)
                ? existingRegistrations ?? new List<DesktopRegistration>()
                : new List<DesktopRegistration>();

            int existingIndex = registrations.FindIndex(existingRegistration =>
                string.Equals(existingRegistration.UserKey, registration.UserKey, StringComparison.Ordinal));

            if (existingIndex >= 0)
            {
                registrations[existingIndex] = registration;
            }
            else
            {
                registrations.Add(registration);
            }

            List<Dictionary<string, object?>> registrationsForStorage = registrations
                .Select(CreateStorageRegistrationObject)
                .ToList();

            string json = JsonSerializer.Serialize(registrationsForStorage, ConfigJsonOptions);
            File.WriteAllText(filePath, json);

            Debug.WriteLine($"Config saved: {filePath}");
        }

        public void UpsertNamespace(string userId, NamespaceItemDto namespaceItem)
        {
            if (string.IsNullOrWhiteSpace(namespaceItem.NamespaceId))
            {
                throw new ArgumentException("Namespace id must not be empty.", nameof(namespaceItem));
            }

            DesktopRegistration registration = ReadRegistration(userId);

            NamespaceItemDto? existingNamespace = FindNamespace(registration, namespaceItem.NamespaceId);

            if (existingNamespace == null)
            {
                registration.Namespaces.Add(new NamespaceItemDto
                {
                    NamespaceId = namespaceItem.NamespaceId,
                    Namespace = namespaceItem.Namespace,
                    Url = namespaceItem.Url,
                    DesktopDeviceId = namespaceItem.DesktopDeviceId,
                    DeviceName = namespaceItem.DeviceName,
                    AccountId = namespaceItem.AccountId,
                    Id = namespaceItem.Id,
                    EncDek = namespaceItem.EncDek
                });
            }
            else
            {
                existingNamespace.Namespace = namespaceItem.Namespace;
                existingNamespace.Url = namespaceItem.Url;

                if (!string.IsNullOrWhiteSpace(namespaceItem.DesktopDeviceId))
                {
                    existingNamespace.DesktopDeviceId = namespaceItem.DesktopDeviceId;
                }

                if (!string.IsNullOrWhiteSpace(namespaceItem.DeviceName))
                {
                    existingNamespace.DeviceName = namespaceItem.DeviceName;
                }

                if (!string.IsNullOrWhiteSpace(namespaceItem.AccountId))
                {
                    existingNamespace.AccountId = namespaceItem.AccountId;
                }

                if (namespaceItem.Id > 0)
                {
                    existingNamespace.Id = namespaceItem.Id;
                }

                if (!string.IsNullOrWhiteSpace(namespaceItem.EncDek))
                {
                    existingNamespace.EncDek = namespaceItem.EncDek;
                }
            }

            SaveRegistration(registration);
            Debug.WriteLine($"Namespace upserted: {namespaceItem.NamespaceId} - {namespaceItem.Namespace}");
        }

        public void RemoveNamespace(string userId, string namespaceId)
        {
            DesktopRegistration registration = ReadRegistration(userId);
            NamespaceItemDto? namespaceItem = FindNamespace(registration, namespaceId);

            if (namespaceItem == null)
            {
                return;
            }

            registration.Namespaces.Remove(namespaceItem);

            if (registration.Namespaces.Count == 0)
            {
                RemoveRegistration(registration.UserKey);
                Debug.WriteLine($"Registration removed after its final namespace was deleted: {registration.UserKey}");
                return;
            }

            SaveRegistration(registration);
            Debug.WriteLine($"Namespace removed: {namespaceId}");
        }

        public void RenameRegistration(string userId, string userName)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userName))
            {
                throw new ArgumentException("Profile id and account name must not be empty.");
            }

            List<DesktopRegistration> registrations = ReadRegistrations().ToList();
            DesktopRegistration registration = registrations.FirstOrDefault(item =>
                string.Equals(item.UserKey, userId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("The selected profile could not be found.");
            registration.UserName = userName.Trim();
            string json = JsonSerializer.Serialize(
                registrations.Select(CreateStorageRegistrationObject).ToList(), ConfigJsonOptions);
            File.WriteAllText(filePath, json);
        }

        public bool RemoveRegistration(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                !TryReadRegistrations(out List<DesktopRegistration>? registrations) ||
                registrations == null)
            {
                return false;
            }

            int removedCount = registrations.RemoveAll(registration =>
                string.Equals(registration.UserKey, userId, StringComparison.Ordinal));
            if (removedCount == 0)
            {
                return false;
            }

            List<Dictionary<string, object?>> registrationsForStorage = registrations
                .Select(CreateStorageRegistrationObject)
                .ToList();
            string json = JsonSerializer.Serialize(registrationsForStorage, ConfigJsonOptions);
            File.WriteAllText(filePath, json);
            Debug.WriteLine($"Registration removed: {userId}");
            return true;
        }

        public void UpdateNamespaceAccountId(string userId, string namespaceId, string accountId, string? namespaceName = null)
        {
            if (string.IsNullOrWhiteSpace(namespaceId))
            {
                throw new ArgumentException("Namespace id must not be empty.", nameof(namespaceId));
            }

            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("Account id must not be empty.", nameof(accountId));
            }

            DesktopRegistration registration = ReadRegistration(userId);
            NamespaceItemDto namespaceItem = FindNamespace(registration, namespaceId)
                ?? throw new InvalidOperationException($"Namespace '{namespaceId}' was not found in config.json.");

            namespaceItem.AccountId = accountId;

            if (!string.IsNullOrWhiteSpace(namespaceName))
            {
                namespaceItem.Namespace = namespaceName;
            }

            SaveRegistration(registration);
            Debug.WriteLine($"Namespace account updated: {namespaceId} -> {accountId}, namespace='{namespaceItem.Namespace}'");
        }

        public void UpdateNamespaceRegistration(string userId, string existingNamespaceId, string resolvedNamespaceId, string accountId, string? namespaceName = null, string? encDek = null)
        {
            if (string.IsNullOrWhiteSpace(existingNamespaceId))
            {
                throw new ArgumentException("Existing namespace id must not be empty.", nameof(existingNamespaceId));
            }

            if (string.IsNullOrWhiteSpace(resolvedNamespaceId))
            {
                throw new ArgumentException("Resolved namespace id must not be empty.", nameof(resolvedNamespaceId));
            }

            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("Account id must not be empty.", nameof(accountId));
            }

            DesktopRegistration registration = ReadRegistration(userId);
            NamespaceItemDto namespaceItem = FindNamespace(registration, existingNamespaceId)
                ?? throw new InvalidOperationException($"Namespace '{existingNamespaceId}' was not found in config.json.");

            NamespaceItemDto? conflictingNamespace = registration.Namespaces.FirstOrDefault(item =>
                !ReferenceEquals(item, namespaceItem) &&
                string.Equals(item.NamespaceId, resolvedNamespaceId, StringComparison.Ordinal));

            if (conflictingNamespace != null)
            {
                registration.Namespaces.Remove(namespaceItem);
                SaveRegistration(registration);
                Debug.WriteLine($"Namespace registration conflict cleanup: removed pending namespace '{existingNamespaceId}' because resolved namespace '{resolvedNamespaceId}' already exists.");
                throw new InvalidOperationException($"Namespace '{resolvedNamespaceId}' already exists in config.json.");
            }

            namespaceItem.NamespaceId = resolvedNamespaceId;
            namespaceItem.AccountId = accountId;

            if (!string.IsNullOrWhiteSpace(encDek))
            {
                namespaceItem.EncDek = encDek;
            }

            if (!string.IsNullOrWhiteSpace(namespaceName))
            {
                namespaceItem.Namespace = namespaceName;
            }

            SaveRegistration(registration);
            Debug.WriteLine($"Namespace registration updated: {existingNamespaceId} -> {resolvedNamespaceId}, accountId='{accountId}', namespace='{namespaceItem.Namespace}'");
        }

        public void SaveNamespaces(
            string userId,
            string accountId,
            string desktopDeviceId,
            IReadOnlyCollection<NamespaceItemDto> namespaces)
        {
            Debug.WriteLine($"SaveNamespaces: {filePath}");
            DesktopRegistration registration = ReadRegistration(userId);

            NamespaceItemDto primaryNamespace = registration.Namespaces.FirstOrDefault()
                ?? throw new InvalidDataException("config.json does not contain any namespace entries.");

            if (!string.Equals(primaryNamespace.DesktopDeviceId, desktopDeviceId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The namespace response does not match the registered desktopDeviceId.");
            }

            var syncedNamespaces = new List<NamespaceItemDto>();
            var seenNamespaceIds = new HashSet<string>(StringComparer.Ordinal);
            var existingNamespacesById = registration.Namespaces
                .Where(item => !string.IsNullOrWhiteSpace(item.NamespaceId))
                .ToDictionary(item => item.NamespaceId!, StringComparer.Ordinal);

            foreach (NamespaceItemDto incomingNamespace in namespaces)
            {
                if (string.IsNullOrWhiteSpace(incomingNamespace.NamespaceId))
                {
                    continue;
                }

                string namespaceId = incomingNamespace.NamespaceId;

                if (!seenNamespaceIds.Add(namespaceId))
                {
                    continue;
                }

                string resolvedAccountId = string.IsNullOrWhiteSpace(incomingNamespace.AccountId)
                    ? accountId
                    : incomingNamespace.AccountId;
                string resolvedDesktopDeviceId = string.IsNullOrWhiteSpace(incomingNamespace.DesktopDeviceId)
                    ? existingNamespacesById.GetValueOrDefault(namespaceId)?.DesktopDeviceId ?? primaryNamespace.DesktopDeviceId
                    : incomingNamespace.DesktopDeviceId;
                string resolvedDeviceName = string.IsNullOrWhiteSpace(incomingNamespace.DeviceName)
                    ? existingNamespacesById.GetValueOrDefault(namespaceId)?.DeviceName
                        ?? (string.Equals(resolvedDesktopDeviceId, primaryNamespace.DesktopDeviceId, StringComparison.Ordinal)
                            ? Environment.MachineName
                            : string.Empty)
                    : incomingNamespace.DeviceName;
                string resolvedEncDek = string.IsNullOrWhiteSpace(incomingNamespace.EncDek)
                    ? existingNamespacesById.GetValueOrDefault(namespaceId)?.EncDek
                        ?? (string.Equals(resolvedDesktopDeviceId, primaryNamespace.DesktopDeviceId, StringComparison.Ordinal)
                            ? primaryNamespace.EncDek
                            : string.Empty)
                    : incomingNamespace.EncDek;

                syncedNamespaces.Add(new NamespaceItemDto
                {
                    Id = incomingNamespace.Id,
                    DesktopDeviceId = resolvedDesktopDeviceId,
                    DeviceName = resolvedDeviceName,
                    AccountId = resolvedAccountId,
                    NamespaceId = namespaceId,
                    Namespace = incomingNamespace.Namespace,
                    Url = incomingNamespace.Url,
                    EncDek = resolvedEncDek
                });

                Debug.WriteLine($"Synced namespace: {namespaceId} - {incomingNamespace.Namespace}");
            }

            registration.Namespaces = syncedNamespaces;

            SaveRegistration(registration);

            Debug.WriteLine($"Config namespaces updated: {filePath}");
        }

        private static bool IsValidRegistration(DesktopRegistration? registration)
        {
            return registration != null &&
                !string.IsNullOrWhiteSpace(registration.UserKey) &&
                registration.Namespaces.Any(IsValidNamespaceCredentials);
        }

        private static Dictionary<string, object?> CreateStorageRegistrationObject(DesktopRegistration registration)
        {
            var storageObject = new Dictionary<string, object?>
            {
                ["userId"] = registration.UserId,
                ["userName"] = registration.UserName,
                ["id"] = registration.Id,
                ["namespaces"] = registration.Namespaces
            };

            return storageObject;
        }

        private static bool IsValidNamespaceCredentials(NamespaceItemDto namespaceItem)
        {
            return !string.IsNullOrWhiteSpace(namespaceItem.NamespaceId) &&
                   !string.IsNullOrWhiteSpace(namespaceItem.Url) &&
                   !string.IsNullOrWhiteSpace(namespaceItem.AccountId) &&
                   !string.IsNullOrWhiteSpace(namespaceItem.DesktopDeviceId) &&
                   !string.IsNullOrWhiteSpace(namespaceItem.EncDek);
        }

        private static NamespaceItemDto? FindNamespace(DesktopRegistration registration, string namespaceId)
        {
            return registration.Namespaces.FirstOrDefault(item =>
                string.Equals(item.NamespaceId, namespaceId, StringComparison.Ordinal));
        }

        private static string NormalizeUrlForComparison(string url)
        {
            return string.IsNullOrWhiteSpace(url)
                ? string.Empty
                : url.Trim().TrimEnd('/');
        }

        private static DesktopRegistration? SelectRegistration(
            IEnumerable<DesktopRegistration> registrations,
            string? userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                DesktopRegistration? selectedRegistration = registrations.FirstOrDefault(registration =>
                    string.Equals(registration.UserKey, userId, StringComparison.Ordinal));

                if (selectedRegistration != null)
                {
                    return selectedRegistration;
                }
            }

            return registrations.FirstOrDefault();
        }

        private static string EnsureUserId(DesktopRegistration registration)
        {
            if (!string.IsNullOrWhiteSpace(registration.UserId))
            {
                return registration.UserId;
            }

            string? primaryDesktopDeviceId = registration.Namespaces.FirstOrDefault()?.DesktopDeviceId;
            if (!string.IsNullOrWhiteSpace(primaryDesktopDeviceId))
            {
                return primaryDesktopDeviceId;
            }

            if (!string.IsNullOrWhiteSpace(registration.DesktopDeviceId))
            {
                return registration.DesktopDeviceId;
            }

            return Guid.NewGuid().ToString("D");
        }

    }
}