using System.Diagnostics;
using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class RegistrationConfigStore
    {
        private readonly string filePath = AppPaths.GetSourceRootFilePath("config.json");

        private static readonly JsonSerializerOptions BootstrapConfigJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions ConfigJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public bool FileExists => File.Exists(filePath);

        public ConfigModel LoadBootstrapConfig()
        {
            Debug.WriteLine($"Bootstrap config loaded: {filePath}");

            if (!File.Exists(filePath))
            {
                return new ConfigModel();
            }

            string json = File.ReadAllText(filePath);

            if (TryParseRegistrations(json, out List<DesktopRegistration>? registrations) &&
                registrations is { Count: > 0 })
            {
                return new ConfigModel
                {
                    BaseUrl = registrations[0].Namespaces.FirstOrDefault()?.Url ?? string.Empty
                };
            }

            return JsonSerializer.Deserialize<ConfigModel>(json, BootstrapConfigJsonOptions) ?? new ConfigModel();
        }

        public void SaveBootstrapConfig(ConfigModel config)
        {
            if (TryReadRegistrations(out _))
            {
                Debug.WriteLine("Bootstrap config save skipped because config.json already contains registrations.");
                return;
            }

            string json = JsonSerializer.Serialize(config, ConfigJsonOptions);
            File.WriteAllText(filePath, json);
            Debug.WriteLine($"{config.AccountId}");
            Debug.WriteLine($"{config.DesktopDeviceId}");
            Debug.WriteLine($"Bootstrap config saved: {filePath}");
        }

        public bool TryReadRegistrations(out List<DesktopRegistration>? registrations)
        {
            registrations = null;

            if (!File.Exists(filePath))
            {
                return false;
            }

            string json = File.ReadAllText(filePath);
            return TryParseRegistrations(json, out registrations);
        }

        public IReadOnlyList<DesktopRegistration> ReadRegistrations()
        {
            if (!TryReadRegistrations(out List<DesktopRegistration>? registrations) || registrations is not { Count: > 0 })
            {
                throw new InvalidDataException("config.json does not contain any valid registrations.");
            }

            return registrations;
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

            EnsureNoCrossUserNamespaceConflicts(registrations);

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

            NamespaceItemDto? existingNamespace = registration.Namespaces.FirstOrDefault(
                item => string.Equals(item.NamespaceId, namespaceItem.NamespaceId, StringComparison.Ordinal));

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
                    Id = namespaceItem.Id
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
            }

            SaveRegistration(registration);
            Debug.WriteLine($"Namespace upserted: {namespaceItem.NamespaceId} - {namespaceItem.Namespace}");
        }

        public void UpdateNamespaceAccountId(string userId, string namespaceId, string accountId)
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
            NamespaceItemDto namespaceItem = registration.Namespaces.FirstOrDefault(
                item => string.Equals(item.NamespaceId, namespaceId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Namespace '{namespaceId}' was not found in config.json.");

            namespaceItem.AccountId = accountId;

            SaveRegistration(registration);
            Debug.WriteLine($"Namespace account updated: {namespaceId} -> {accountId}");
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

                syncedNamespaces.Add(new NamespaceItemDto
                {
                    Id = incomingNamespace.Id,
                    DesktopDeviceId = resolvedDesktopDeviceId,
                    DeviceName = resolvedDeviceName,
                    AccountId = resolvedAccountId,
                    NamespaceId = namespaceId,
                    Namespace = incomingNamespace.Namespace,
                    Url = incomingNamespace.Url
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

        private static bool TryParseRegistrations(string json, out List<DesktopRegistration>? registrations)
        {
            registrations = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    registrations = JsonSerializer.Deserialize<List<DesktopRegistration>>(json, BootstrapConfigJsonOptions)
                        ?? new List<DesktopRegistration>();
                }
                else if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    DesktopRegistration? registration = Transformer.ToRegistrationResponse(json);

                    if (!IsValidRegistration(registration))
                    {
                        return false;
                    }

                    registrations = new List<DesktopRegistration> { registration! };
                }
                else
                {
                    return false;
                }
            }
            catch (JsonException)
            {
                return false;
            }

            registrations = registrations
                .Where(IsValidRegistration)
                .ToList();

            return registrations.Count > 0;
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

        private static void EnsureNoCrossUserNamespaceConflicts(IReadOnlyCollection<DesktopRegistration> registrations)
        {
            var ownershipByNamespaceKey = new Dictionary<string, DesktopRegistration>(StringComparer.OrdinalIgnoreCase);

            foreach (DesktopRegistration registration in registrations)
            {
                string ownerKey = registration.UserKey;

                foreach (NamespaceItemDto namespaceItem in registration.Namespaces)
                {
                    if (string.IsNullOrWhiteSpace(namespaceItem.NamespaceId) || string.IsNullOrWhiteSpace(namespaceItem.Url))
                    {
                        continue;
                    }

                    string namespaceKey = CreateNamespaceOwnershipKey(namespaceItem.NamespaceId, namespaceItem.Url);

                    if (ownershipByNamespaceKey.TryGetValue(namespaceKey, out DesktopRegistration? existingOwner) &&
                        !string.Equals(existingOwner.UserKey, ownerKey, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Namespace '{namespaceItem.NamespaceId}' at '{NormalizeNamespaceUrl(namespaceItem.Url)}' is already registered for user '{existingOwner.DisplayUserName}'.");
                    }

                    ownershipByNamespaceKey[namespaceKey] = registration;
                }
            }
        }

        private static string CreateNamespaceOwnershipKey(string namespaceId, string url)
        {
            return $"{namespaceId.Trim()}|{NormalizeNamespaceUrl(url)}";
        }

        private static string NormalizeNamespaceUrl(string url)
        {
            return url.Trim().TrimEnd('/');
        }
    }
}