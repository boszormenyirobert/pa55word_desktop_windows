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
            DesktopRegistration registration = _configStore.ReadRegistration();

            string keyContainer = "KeyContainer_" + registration.DesktopDeviceId;

            byte[] decrypted = new Pki().DecryptWithPrivateKeyOaepSha256(
                keyContainer,
                registration.EncDek);

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