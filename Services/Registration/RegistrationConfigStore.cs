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
                throw new InvalidDataException("config.json does not contain a valid accountId/desktopDeviceId/encDek.");
            }

            return registration!;
        }

        public void SaveRegistration(DesktopRegistration registration)
        {
            registration.UserId = EnsureUserId(registration);

            foreach (NamespaceItemDto namespaceItem in registration.Namespaces)
            {
                if (string.IsNullOrWhiteSpace(namespaceItem.DesktopDeviceId))
                {
                    namespaceItem.DesktopDeviceId = registration.DesktopDeviceId;
                }

                if (string.IsNullOrWhiteSpace(namespaceItem.EncDek) &&
                    string.Equals(namespaceItem.DesktopDeviceId, registration.DesktopDeviceId, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(registration.EncDek))
                {
                    namespaceItem.EncDek = registration.EncDek;
                }

                if (string.IsNullOrWhiteSpace(namespaceItem.DeviceName) &&
                    string.Equals(namespaceItem.DesktopDeviceId, registration.DesktopDeviceId, StringComparison.Ordinal))
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

            string json = JsonSerializer.Serialize(registrations, ConfigJsonOptions);
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

            if (!string.Equals(registration.DesktopDeviceId, desktopDeviceId, StringComparison.Ordinal))
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
                    ? existingNamespacesById.GetValueOrDefault(namespaceId)?.DesktopDeviceId ?? registration.DesktopDeviceId
                    : incomingNamespace.DesktopDeviceId;
                string resolvedDeviceName = string.IsNullOrWhiteSpace(incomingNamespace.DeviceName)
                    ? existingNamespacesById.GetValueOrDefault(namespaceId)?.DeviceName
                        ?? (string.Equals(resolvedDesktopDeviceId, registration.DesktopDeviceId, StringComparison.Ordinal)
                            ? Environment.MachineName
                            : string.Empty)
                    : incomingNamespace.DeviceName;
                string resolvedEncDek = string.IsNullOrWhiteSpace(incomingNamespace.EncDek)
                    ? existingNamespacesById.GetValueOrDefault(namespaceId)?.EncDek
                        ?? (string.Equals(resolvedDesktopDeviceId, registration.DesktopDeviceId, StringComparison.Ordinal)
                            ? registration.EncDek
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
                !string.IsNullOrWhiteSpace(registration.AccountId) &&
                !string.IsNullOrWhiteSpace(registration.DesktopDeviceId) &&
                !string.IsNullOrWhiteSpace(registration.EncDek);
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

            if (!string.IsNullOrWhiteSpace(registration.DesktopDeviceId))
            {
                return registration.DesktopDeviceId;
            }

            return Guid.NewGuid().ToString("D");
        }

    }
}