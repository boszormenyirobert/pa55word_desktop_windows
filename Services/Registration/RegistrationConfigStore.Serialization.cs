using System.Diagnostics;
using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed partial class RegistrationConfigStore
    {
        private static readonly JsonSerializerOptions BootstrapConfigJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
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

            if (string.IsNullOrWhiteSpace(json))
            {
                return new ConfigModel();
            }

            if (TryParseRegistrations(json, out List<DesktopRegistration>? registrations) &&
                registrations is { Count: > 0 })
            {
                return new ConfigModel
                {
                    BaseUrl = registrations[0].Namespaces.FirstOrDefault()?.Url ?? string.Empty
                };
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return new ConfigModel();
                }

                return JsonSerializer.Deserialize<ConfigModel>(json, BootstrapConfigJsonOptions) ?? new ConfigModel();
            }
            catch (JsonException)
            {
                return new ConfigModel();
            }
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

        internal static DesktopRegistration? ParseRegistrationResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            RootDeviceRegistrationResponseDto? response = JsonSerializer.Deserialize<RootDeviceRegistrationResponseDto>(
                json, BootstrapConfigJsonOptions);
            if (response?.Dek != null)
            {
                response.Dek.Namespaces = response.Namespaces ?? new List<NamespaceItemDto>();
                return response.Dek;
            }

            return JsonSerializer.Deserialize<DesktopRegistration>(json, BootstrapConfigJsonOptions);
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
                    DesktopRegistration? registration = ParseRegistrationResponse(json);

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
    }
}