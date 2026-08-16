using TrayApp.Models;

namespace TrayApp.Services
{
    internal static class AppEnvironment
    {
        private const string ConfigFileName = "config.json";
        private static readonly object SelectionLock = new();
        private static readonly RegistrationConfigStore ConfigStore = new();
        private static string? _activeNamespaceId;

        public static string BackendBaseUrl => ResolveBackendBaseUrl();

        public static string BuildApiUrl(string apiRoute)
        {
            string normalizedApiRoute = apiRoute.TrimStart('/');

            return $"{BackendBaseUrl}{ApiRoutes.VersionPrefix}/{normalizedApiRoute}";
        }

        public static string? GetActiveNamespaceId()
        {
            lock (SelectionLock)
            {
                return _activeNamespaceId;
            }
        }

        public static void SetActiveNamespace(string namespaceId)
        {
            if (string.IsNullOrWhiteSpace(namespaceId))
            {
                throw new ArgumentException("Namespace id must not be empty.", nameof(namespaceId));
            }

            DesktopRegistration registration = ConfigStore.ReadRegistration();
            NamespaceItemDto selectedNamespace = registration.Namespaces.FirstOrDefault(
                item => string.Equals(item.NamespaceId, namespaceId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Namespace '{namespaceId}' was not found in config.json.");

            if (string.IsNullOrWhiteSpace(selectedNamespace.Url))
            {
                throw new InvalidDataException($"Namespace '{selectedNamespace.Namespace}' does not contain a valid url value.");
            }

            lock (SelectionLock)
            {
                _activeNamespaceId = selectedNamespace.NamespaceId;
            }
        }

        public static string GetActiveNamespaceAccountIdOrDefault(string fallbackAccountId)
        {
            if (string.IsNullOrWhiteSpace(fallbackAccountId))
            {
                throw new ArgumentException("Fallback account id must not be empty.", nameof(fallbackAccountId));
            }

            if (!ConfigStore.TryReadRegistration(out DesktopRegistration? registration) || registration is not { Namespaces.Count: > 0 })
            {
                return fallbackAccountId;
            }

            NamespaceItemDto activeNamespace = GetActiveNamespace();
            return string.IsNullOrWhiteSpace(activeNamespace.AccountId)
                ? fallbackAccountId
                : activeNamespace.AccountId;
        }

        public static string GetActiveNamespaceDeviceIdOrDefault(string fallbackDeviceId)
        {
            if (string.IsNullOrWhiteSpace(fallbackDeviceId))
            {
                throw new ArgumentException("Fallback device id must not be empty.", nameof(fallbackDeviceId));
            }

            if (!ConfigStore.TryReadRegistration(out DesktopRegistration? registration) || registration is not { Namespaces.Count: > 0 })
            {
                return fallbackDeviceId;
            }

            NamespaceItemDto activeNamespace = GetActiveNamespace();
            return string.IsNullOrWhiteSpace(activeNamespace.DeviceId)
                ? fallbackDeviceId
                : activeNamespace.DeviceId;
        }

        private static NamespaceItemDto GetActiveNamespace()
        {
            DesktopRegistration registration = ConfigStore.ReadRegistration();

            if (registration.Namespaces.Count == 0)
            {
                throw new InvalidDataException(
                    $"Config file '{ConfigFileName}' does not contain any namespaces entries.");
            }

            lock (SelectionLock)
            {
                NamespaceItemDto? selectedNamespace = registration.Namespaces.FirstOrDefault(
                    item => string.Equals(item.NamespaceId, _activeNamespaceId, StringComparison.Ordinal));

                if (selectedNamespace == null)
                {
                    selectedNamespace = registration.Namespaces[0];
                    _activeNamespaceId = selectedNamespace.NamespaceId;
                }

                if (string.IsNullOrWhiteSpace(selectedNamespace.Url))
                {
                    throw new InvalidDataException(
                        $"Namespace '{selectedNamespace.Namespace}' does not contain a valid url value.");
                }

                return selectedNamespace;
            }
        }

        private static string ResolveBackendBaseUrl()
        {
            if (ConfigStore.TryReadRegistration(out DesktopRegistration? registration) && registration is { Namespaces.Count: > 0 })
            {
                return GetActiveNamespace().Url.TrimEnd('/');
            }

            string bootstrapBaseUrl = ConfigStore.LoadBootstrapConfig().BaseUrl;

            if (string.IsNullOrWhiteSpace(bootstrapBaseUrl))
            {
                throw new InvalidDataException(
                    $"Config file '{ConfigFileName}' does not contain a valid baseUrl or namespaces[0].url value.");
            }

            return bootstrapBaseUrl.TrimEnd('/');
        }
    }
}
