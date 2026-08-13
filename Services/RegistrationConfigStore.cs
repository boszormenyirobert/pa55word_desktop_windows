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
            string json = JsonSerializer.Serialize(registration, ConfigJsonOptions);
            File.WriteAllText(filePath, json);

            Debug.WriteLine($"Config saved: {filePath}");
        }

        public void SaveNamespaces(
            string accountId,
            string deviceId,
            IReadOnlyCollection<NamespaceItemDto> namespaces)
        {
            DesktopRegistration registration = ReadRegistration();

            if (!string.Equals(registration.AccountId, accountId, StringComparison.Ordinal) ||
                !string.Equals(registration.DeviceId, deviceId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The namespace response does not match the registered accountId/deviceId.");
            }

            registration.Namespaces = namespaces.ToList();
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