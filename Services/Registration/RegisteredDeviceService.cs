using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class RegisteredDeviceService
    {
        private readonly RegistrationConfigStore _configStore = new();

        public bool TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations)
        {
            registrations = null;

            if (!_configStore.TryReadRegistrations(out List<DesktopRegistration>? parsedRegistrations))
            {
                return false;
            }

            registrations = parsedRegistrations;
            return true;
        }

        public IReadOnlyList<DesktopRegistration> ReadRegistrations()
        {
            return _configStore.ReadRegistrations();
        }

        public bool TryReadRegistration(out DesktopRegistration? registration)
        {
            return _configStore.TryReadRegistration(AppEnvironment.GetActiveUserId(), out registration);
        }

        public DesktopRegistration ReadRegistration()
        {
            return _configStore.ReadRegistration(AppEnvironment.GetActiveUserId());
        }

        public (DesktopRegistration Registration, byte[] DecryptedDek) ReadRegistrationAndDecryptDek()
        {
            DesktopRegistration registration = _configStore.ReadRegistration(AppEnvironment.GetActiveUserId());
            string activeNamespaceId = AppEnvironment.GetActiveNamespaceId() ?? string.Empty;
            NamespaceItemDto? activeNamespace = registration.Namespaces.FirstOrDefault(namespaceItem =>
                string.Equals(namespaceItem.NamespaceId, activeNamespaceId, StringComparison.Ordinal));

            string desktopDeviceIdForDek = string.IsNullOrWhiteSpace(activeNamespace?.DesktopDeviceId)
                ? registration.DesktopDeviceId
                : activeNamespace.DesktopDeviceId;
            if (activeNamespace != null &&
                !string.IsNullOrWhiteSpace(activeNamespace.DesktopDeviceId) &&
                !string.Equals(activeNamespace.DesktopDeviceId, registration.DesktopDeviceId, StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(activeNamespace.EncDek))
            {
                throw new InvalidOperationException(
                    $"Namespace '{activeNamespace.NamespaceId}' does not contain encrypted DEK for desktop device '{activeNamespace.DesktopDeviceId}'. Re-sync or re-register this namespace.");
            }

            string encDekForNamespace = string.IsNullOrWhiteSpace(activeNamespace?.EncDek)
                ? registration.EncDek
                : activeNamespace.EncDek;

            string keyContainer = "KeyContainer_" + desktopDeviceIdForDek;

            byte[] decrypted = new Pki().DecryptWithPrivateKeyOaepSha256(
                keyContainer,
                encDekForNamespace);

            return (registration, decrypted);
        }

        public void SaveNamespaces(
            string accountId,
            string desktopDeviceId,
            IReadOnlyCollection<NamespaceItemDto> namespaces)
        {
            _configStore.SaveNamespaces(AppEnvironment.GetActiveUserId() ?? string.Empty, accountId, desktopDeviceId, namespaces);
        }
    }
}