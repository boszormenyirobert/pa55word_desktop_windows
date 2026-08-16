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
            return JsonSerializer.Deserialize<ConfigModel>(json, BootstrapConfigJsonOptions) ?? new ConfigModel();
        }

        public void SaveBootstrapConfig(ConfigModel config)
        {
            string json = JsonSerializer.Serialize(config, ConfigJsonOptions);
            File.WriteAllText(filePath, json);
            Debug.WriteLine($"{config.AccountId}");
            Debug.WriteLine($"{config.DeviceId}");
            Debug.WriteLine($"Bootstrap config saved: {filePath}");
        }

        public bool TryReadRegistration(out DesktopRegistration? registration)
        {
            registration = null;

            if (!File.Exists(filePath))
            {
                return false;
            }

            string json = File.ReadAllText(filePath);
            DesktopRegistration? parsedRegistration = Transformer.ToRegistrationResponse(json);

            if (!IsValidRegistration(parsedRegistration))
            {
                return false;
            }

            registration = parsedRegistration;
            return true;
        }

        public DesktopRegistration ReadRegistration()
        {
            if (!TryReadRegistration(out DesktopRegistration? registration))
            {
                throw new InvalidDataException("config.json does not contain a valid accountId/deviceId/encDek.");
            }

            return registration!;
        }

        public void SaveRegistration(DesktopRegistration registration)
        {
            foreach (NamespaceItemDto namespaceItem in registration.Namespaces)
            {
                if (string.IsNullOrWhiteSpace(namespaceItem.DeviceId))
                {
                    namespaceItem.DeviceId = registration.DeviceId;
                }
            }

            string json = JsonSerializer.Serialize(registration, ConfigJsonOptions);
            File.WriteAllText(filePath, json);

            Debug.WriteLine($"Config saved: {filePath}");
        }

        public void UpsertNamespace(NamespaceItemDto namespaceItem)
        {
            if (string.IsNullOrWhiteSpace(namespaceItem.NamespaceId))
            {
                throw new ArgumentException("Namespace id must not be empty.", nameof(namespaceItem));
            }

            DesktopRegistration registration = ReadRegistration();

            NamespaceItemDto? existingNamespace = registration.Namespaces.FirstOrDefault(
                item => string.Equals(item.NamespaceId, namespaceItem.NamespaceId, StringComparison.Ordinal));

            if (existingNamespace == null)
            {
                registration.Namespaces.Add(new NamespaceItemDto
                {
                    NamespaceId = namespaceItem.NamespaceId,
                    Namespace = namespaceItem.Namespace,
                    Url = namespaceItem.Url,
                    DeviceId = namespaceItem.DeviceId,
                    DeviceName = namespaceItem.DeviceName,
                    AccountId = namespaceItem.AccountId,
                    Id = namespaceItem.Id
                });
            }
            else
            {
                existingNamespace.Namespace = namespaceItem.Namespace;
                existingNamespace.Url = namespaceItem.Url;

                if (!string.IsNullOrWhiteSpace(namespaceItem.DeviceId))
                {
                    existingNamespace.DeviceId = namespaceItem.DeviceId;
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

        public void UpdateNamespaceAccountId(string namespaceId, string accountId)
        {
            if (string.IsNullOrWhiteSpace(namespaceId))
            {
                throw new ArgumentException("Namespace id must not be empty.", nameof(namespaceId));
            }

            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("Account id must not be empty.", nameof(accountId));
            }

            DesktopRegistration registration = ReadRegistration();
            NamespaceItemDto namespaceItem = registration.Namespaces.FirstOrDefault(
                item => string.Equals(item.NamespaceId, namespaceId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Namespace '{namespaceId}' was not found in config.json.");

            namespaceItem.AccountId = accountId;

            SaveRegistration(registration);
            Debug.WriteLine($"Namespace account updated: {namespaceId} -> {accountId}");
        }

        public void SaveNamespaces(
            string accountId,
            string deviceId,
            IReadOnlyCollection<NamespaceItemDto> namespaces)
        {
            Debug.WriteLine($"SaveNamespaces: {filePath}");
            DesktopRegistration registration = ReadRegistration();

            if (!string.Equals(registration.DeviceId, deviceId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The namespace response does not match the registered deviceId.");
            }

            var syncedNamespaces = new List<NamespaceItemDto>();
            var seenNamespaceIds = new HashSet<string>(StringComparer.Ordinal);
            var existingNamespacesById = registration.Namespaces
                .Where(item => !string.IsNullOrWhiteSpace(item.NamespaceId))
                .ToDictionary(item => item.NamespaceId, StringComparer.Ordinal);

            foreach (NamespaceItemDto incomingNamespace in namespaces)
            {
                if (string.IsNullOrWhiteSpace(incomingNamespace.NamespaceId))
                {
                    continue;
                }

                if (!seenNamespaceIds.Add(incomingNamespace.NamespaceId))
                {
                    continue;
                }

                string resolvedAccountId = string.IsNullOrWhiteSpace(incomingNamespace.AccountId)
                    ? accountId
                    : incomingNamespace.AccountId;
                string resolvedDeviceId = string.IsNullOrWhiteSpace(incomingNamespace.DeviceId)
                    ? existingNamespacesById.GetValueOrDefault(incomingNamespace.NamespaceId)?.DeviceId ?? registration.DeviceId
                    : incomingNamespace.DeviceId;
                string resolvedDeviceName = string.IsNullOrWhiteSpace(incomingNamespace.DeviceName)
                    ? existingNamespacesById.GetValueOrDefault(incomingNamespace.NamespaceId)?.DeviceName ?? string.Empty
                    : incomingNamespace.DeviceName;

                syncedNamespaces.Add(new NamespaceItemDto
                {
                    Id = incomingNamespace.Id,
                    DeviceId = resolvedDeviceId,
                    DeviceName = resolvedDeviceName,
                    AccountId = resolvedAccountId,
                    NamespaceId = incomingNamespace.NamespaceId,
                    Namespace = incomingNamespace.Namespace,
                    Url = incomingNamespace.Url
                });

                Debug.WriteLine($"Synced namespace: {incomingNamespace.NamespaceId} - {incomingNamespace.Namespace}");
            }

            registration.Namespaces = syncedNamespaces;

            SaveRegistration(registration);

            Debug.WriteLine($"Config namespaces updated: {filePath}");
        }

        private static bool IsValidRegistration(DesktopRegistration? registration)
        {
            return registration != null &&
                !string.IsNullOrWhiteSpace(registration.AccountId) &&
                !string.IsNullOrWhiteSpace(registration.DeviceId) &&
                !string.IsNullOrWhiteSpace(registration.EncDek);
        }
    }
}