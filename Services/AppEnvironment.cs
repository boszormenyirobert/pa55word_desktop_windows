using TrayApp.Models;

namespace TrayApp.Services
{
    internal static class AppEnvironment
    {
        private const string ConfigFileName = "config.json";
        private static readonly object SelectionLock = new();
        private static readonly RegistrationConfigStore ConfigStore = new();
        private static string? _activeUserId;
        private static string? _activeNamespaceId;

        public static string BackendBaseUrl => ResolveBackendBaseUrl();

        public static string BuildApiUrl(string apiRoute)
        {
            string normalizedApiRoute = apiRoute.TrimStart('/');

            return $"{BackendBaseUrl}{ApiRoutes.VersionPrefix}/{normalizedApiRoute}";
        }

        public static string BuildApiUrl(string baseUrl, string apiRoute)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base url must not be empty.", nameof(baseUrl));
            }

            string normalizedApiRoute = apiRoute.TrimStart('/');
            return $"{baseUrl.TrimEnd('/')}{ApiRoutes.VersionPrefix}/{normalizedApiRoute}";
        }

        public static string? GetActiveNamespaceId()
        {
            lock (SelectionLock)
            {
                return _activeNamespaceId;
            }
        }

        public static string? GetActiveUserId()
        {
            lock (SelectionLock)
            {
                return _activeUserId;
            }
        }

        public static void SetActiveUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User id must not be empty.", nameof(userId));
            }

            DesktopRegistration registration = GetRegistration(userId);

            lock (SelectionLock)
            {
                _activeUserId = registration.UserKey;

                NamespaceItemDto? selectedNamespace = registration.Namespaces.FirstOrDefault(namespaceItem =>
                    string.Equals(namespaceItem.NamespaceId, _activeNamespaceId, StringComparison.Ordinal));

                _activeNamespaceId = selectedNamespace?.NamespaceId
                    ?? registration.Namespaces.FirstOrDefault()?.NamespaceId;
            }
        }

        public static void SetActiveNamespace(string namespaceId)
        {
            if (string.IsNullOrWhiteSpace(namespaceId))
            {
                throw new ArgumentException("Namespace id must not be empty.", nameof(namespaceId));
            }

            DesktopRegistration registration = GetActiveRegistration();
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

            if (!TryReadActiveRegistration(out DesktopRegistration? registration) || registration is not { Namespaces.Count: > 0 })
            {
                return fallbackAccountId;
            }

            NamespaceItemDto activeNamespace = GetActiveNamespace();
            return string.IsNullOrWhiteSpace(activeNamespace.AccountId)
                ? fallbackAccountId
                : activeNamespace.AccountId;
        }

        public static string GetPrimaryNamespaceAccountIdOrDefault(string fallbackAccountId)
        {
            if (string.IsNullOrWhiteSpace(fallbackAccountId))
            {
                throw new ArgumentException("Fallback account id must not be empty.", nameof(fallbackAccountId));
            }

            if (!TryReadActiveRegistration(out DesktopRegistration? registration) || registration is not { Namespaces.Count: > 0 })
            {
                return fallbackAccountId;
            }

            NamespaceItemDto primaryNamespace = registration.Namespaces[0];
            return string.IsNullOrWhiteSpace(primaryNamespace.AccountId)
                ? fallbackAccountId
                : primaryNamespace.AccountId;
        }

        public static string GetActiveNamespaceDesktopDeviceIdOrDefault(string fallbackDesktopDeviceId)
        {
            if (string.IsNullOrWhiteSpace(fallbackDesktopDeviceId))
            {
                throw new ArgumentException("Fallback desktop device id must not be empty.", nameof(fallbackDesktopDeviceId));
            }

            if (!TryReadActiveRegistration(out DesktopRegistration? registration) || registration is not { Namespaces.Count: > 0 })
            {
                return fallbackDesktopDeviceId;
            }

            NamespaceItemDto activeNamespace = GetActiveNamespace();
            return string.IsNullOrWhiteSpace(activeNamespace.DesktopDeviceId)
                ? fallbackDesktopDeviceId
                : activeNamespace.DesktopDeviceId;
        }

        public static string GetPrimaryNamespaceDesktopDeviceIdOrDefault(string fallbackDesktopDeviceId)
        {
            if (string.IsNullOrWhiteSpace(fallbackDesktopDeviceId))
            {
                throw new ArgumentException("Fallback desktop device id must not be empty.", nameof(fallbackDesktopDeviceId));
            }

            if (!TryReadActiveRegistration(out DesktopRegistration? registration) || registration is not { Namespaces.Count: > 0 })
            {
                return fallbackDesktopDeviceId;
            }

            NamespaceItemDto primaryNamespace = registration.Namespaces[0];
            return string.IsNullOrWhiteSpace(primaryNamespace.DesktopDeviceId)
                ? fallbackDesktopDeviceId
                : primaryNamespace.DesktopDeviceId;
        }

        public static string GetActiveNamespaceUrlOrDefault(string fallbackBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(fallbackBaseUrl))
            {
                throw new ArgumentException("Fallback base url must not be empty.", nameof(fallbackBaseUrl));
            }

            if (!TryReadActiveRegistration(out DesktopRegistration? registration) || registration is not { Namespaces.Count: > 0 })
            {
                return fallbackBaseUrl;
            }

            NamespaceItemDto activeNamespace = GetActiveNamespace();
            return string.IsNullOrWhiteSpace(activeNamespace.Url)
                ? fallbackBaseUrl
                : activeNamespace.Url.TrimEnd('/');
        }

        public static string GetPrimaryNamespaceUrlOrDefault(string fallbackBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(fallbackBaseUrl))
            {
                throw new ArgumentException("Fallback base url must not be empty.", nameof(fallbackBaseUrl));
            }

            if (!TryReadActiveRegistration(out DesktopRegistration? registration) || registration is not { Namespaces.Count: > 0 })
            {
                return fallbackBaseUrl;
            }

            NamespaceItemDto primaryNamespace = registration.Namespaces[0];
            return string.IsNullOrWhiteSpace(primaryNamespace.Url)
                ? fallbackBaseUrl
                : primaryNamespace.Url.TrimEnd('/');
        }

        private static NamespaceItemDto GetActiveNamespace()
        {
            DesktopRegistration registration = GetActiveRegistration();

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
            if (TryReadActiveRegistration(out DesktopRegistration? registration) && registration is { Namespaces.Count: > 0 })
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

        private static bool TryReadActiveRegistration(out DesktopRegistration? registration)
        {
            return ConfigStore.TryReadRegistration(GetActiveUserId(), out registration);
        }

        private static DesktopRegistration GetActiveRegistration()
        {
            IReadOnlyList<DesktopRegistration> registrations = ConfigStore.ReadRegistrations();

            lock (SelectionLock)
            {
                DesktopRegistration registration = registrations.FirstOrDefault(candidate =>
                        string.Equals(candidate.UserKey, _activeUserId, StringComparison.Ordinal))
                    ?? registrations[0];

                _activeUserId = registration.UserKey;

                if (registration.Namespaces.Count > 0)
                {
                    NamespaceItemDto? selectedNamespace = registration.Namespaces.FirstOrDefault(namespaceItem =>
                        string.Equals(namespaceItem.NamespaceId, _activeNamespaceId, StringComparison.Ordinal));

                    _activeNamespaceId = selectedNamespace?.NamespaceId ?? registration.Namespaces[0].NamespaceId;
                }
                else
                {
                    _activeNamespaceId = null;
                }

                return registration;
            }
        }

        private static DesktopRegistration GetRegistration(string userId)
        {
            IReadOnlyList<DesktopRegistration> registrations = ConfigStore.ReadRegistrations();

            return registrations.FirstOrDefault(registration =>
                    string.Equals(registration.UserKey, userId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"User '{userId}' was not found in config.json.");
        }
    }
}
