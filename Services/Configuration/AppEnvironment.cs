using TrayApp.Models;

namespace TrayApp.Services
{
    internal static class AppEnvironment
    {
        private const string ConfigFileName = "config.json";
        private static readonly object SelectionLock = new();
        private static readonly RegistrationConfigStore ConfigStore = new();
        private static ActiveRegistrationSelection _activeSelection = new(null, null);

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
                return _activeSelection.NamespaceId;
            }
        }

        public static string? GetActiveUserId()
        {
            lock (SelectionLock)
            {
                return _activeSelection.UserId;
            }
        }

        public static void ClearActiveSelection()
        {
            lock (SelectionLock)
            {
                _activeSelection = new ActiveRegistrationSelection(null, null);
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
                _activeSelection = _activeSelection with { UserId = registration.UserKey };

                NamespaceItemDto? selectedNamespace = registration.Namespaces.FirstOrDefault(namespaceItem =>
                    string.Equals(namespaceItem.NamespaceId, _activeSelection.NamespaceId, StringComparison.Ordinal));

                _activeSelection = _activeSelection with
                {
                    NamespaceId = selectedNamespace?.NamespaceId ?? registration.Namespaces.FirstOrDefault()?.NamespaceId
                };
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
                _activeSelection = _activeSelection with { NamespaceId = selectedNamespace.NamespaceId };
            }
        }

        public static string GetActiveNamespaceAccountId()
        {
            NamespaceItemDto activeNamespace = GetActiveNamespace();

            if (string.IsNullOrWhiteSpace(activeNamespace.AccountId))
            {
                throw new InvalidDataException("Active namespace does not contain a valid accountId value.");
            }

            return activeNamespace.AccountId;
        }

        public static string GetPrimaryNamespaceAccountId()
        {
            DesktopRegistration registration = GetActiveRegistration();

            if (registration.Namespaces.Count == 0)
            {
                throw new InvalidDataException($"Config file '{ConfigFileName}' does not contain any namespaces entries.");
            }

            string accountId = registration.Namespaces[0].AccountId;
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new InvalidDataException("Primary namespace does not contain a valid accountId value.");
            }

            return accountId;
        }

        public static string GetActiveNamespaceDesktopDeviceId()
        {
            NamespaceItemDto activeNamespace = GetActiveNamespace();

            if (string.IsNullOrWhiteSpace(activeNamespace.DesktopDeviceId))
            {
                throw new InvalidDataException("Active namespace does not contain a valid desktopDeviceId value.");
            }

            return activeNamespace.DesktopDeviceId;
        }

        public static string GetPrimaryNamespaceDesktopDeviceId()
        {
            DesktopRegistration registration = GetActiveRegistration();

            if (registration.Namespaces.Count == 0)
            {
                throw new InvalidDataException($"Config file '{ConfigFileName}' does not contain any namespaces entries.");
            }

            string desktopDeviceId = registration.Namespaces[0].DesktopDeviceId;
            if (string.IsNullOrWhiteSpace(desktopDeviceId))
            {
                throw new InvalidDataException("Primary namespace does not contain a valid desktopDeviceId value.");
            }

            return desktopDeviceId;
        }

        public static string GetActiveNamespaceUrl()
        {
            NamespaceItemDto activeNamespace = GetActiveNamespace();

            if (string.IsNullOrWhiteSpace(activeNamespace.Url))
            {
                throw new InvalidDataException("Active namespace does not contain a valid url value.");
            }

            return activeNamespace.Url.TrimEnd('/');
        }

        public static string GetPrimaryNamespaceUrl()
        {
            DesktopRegistration registration = GetActiveRegistration();

            if (registration.Namespaces.Count == 0)
            {
                throw new InvalidDataException($"Config file '{ConfigFileName}' does not contain any namespaces entries.");
            }

            string baseUrl = registration.Namespaces[0].Url;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidDataException("Primary namespace does not contain a valid url value.");
            }

            return baseUrl.TrimEnd('/');
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
                    item => string.Equals(item.NamespaceId, _activeSelection.NamespaceId, StringComparison.Ordinal));

                if (selectedNamespace == null)
                {
                    selectedNamespace = registration.Namespaces[0];
                    _activeSelection = _activeSelection with { NamespaceId = selectedNamespace.NamespaceId };
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
                        string.Equals(candidate.UserKey, _activeSelection.UserId, StringComparison.Ordinal))
                    ?? registrations[0];

                _activeSelection = _activeSelection with { UserId = registration.UserKey };

                if (registration.Namespaces.Count > 0)
                {
                    NamespaceItemDto? selectedNamespace = registration.Namespaces.FirstOrDefault(namespaceItem =>
                        string.Equals(namespaceItem.NamespaceId, _activeSelection.NamespaceId, StringComparison.Ordinal));

                    _activeSelection = _activeSelection with
                    {
                        NamespaceId = selectedNamespace?.NamespaceId ?? registration.Namespaces[0].NamespaceId
                    };
                }
                else
                {
                    _activeSelection = _activeSelection with { NamespaceId = null };
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

        private readonly record struct ActiveRegistrationSelection(string? UserId, string? NamespaceId);
    }
}
