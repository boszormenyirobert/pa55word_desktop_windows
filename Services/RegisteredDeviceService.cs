using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class RegisteredDeviceService
    {
        private readonly RegistrationConfigStore _configStore = new();

        public bool TryReadRegistration(out DesktopRegistration? registration)
        {
            return _configStore.TryReadRegistration(out registration);
        }

        public DesktopRegistration ReadRegistration()
        {
            return _configStore.ReadRegistration();
        }

        public (DesktopRegistration Registration, byte[] DecryptedDek) ReadRegistrationAndDecryptDek()
        {
            DesktopRegistration registration = _configStore.ReadRegistration();

            string keyContainer = "KeyContainer_" + registration.DeviceId;

            byte[] decrypted = new Pki().DecryptWithPrivateKeyOaepSha256(
                keyContainer,
                registration.EncDek);

            return (registration, decrypted);
        }

        public void SaveNamespaces(
            string accountId,
            string deviceId,
            IReadOnlyCollection<NamespaceItemDto> namespaces)
        {
            _configStore.SaveNamespaces(accountId, deviceId, namespaces);
        }
    }
}