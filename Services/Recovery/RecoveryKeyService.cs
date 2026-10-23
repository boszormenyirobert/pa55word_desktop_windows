using System.Security.Cryptography;

namespace TrayApp.Services
{
    internal interface IRecoveryKeyService
    {
        IReadOnlyDictionary<string, string> Sign(string desktopId, string json);
        string ReencryptDek(RecoveryTarget target, string publicKey);
    }

    internal sealed class RecoveryKeyService : IRecoveryKeyService
    {
        public IReadOnlyDictionary<string, string> Sign(string desktopId, string json) =>
            RequestSigner.CreateSignatureHeaders(desktopId, json, "recovery");

        public string ReencryptDek(RecoveryTarget target, string publicKey)
        {
            using RSA mobile = ImportMobileKey(publicKey);
            byte[] dek;
            try { dek = new Pki().DecryptWithPrivateKeyOaepSha256("KeyContainer_" + target.DesktopDeviceId, target.EncDek); }
            catch (FormatException error) { throw new CryptographicException("The registered encrypted DEK is not Base64.", error); }
            try { return EncryptDek(mobile, dek); }
            finally { CryptographicOperations.ZeroMemory(dek); }
        }

        internal static RSA ImportMobileKey(string publicKey)
        {
            if (string.IsNullOrWhiteSpace(publicKey) || publicKey.Length > 1024)
                throw new CryptographicException("The selected mobile public key is invalid.");
            byte[] der;
            try { der = Convert.FromBase64String(publicKey); }
            catch (FormatException error) { throw new CryptographicException("The selected mobile public key is not Base64.", error); }
            RSA rsa = RSA.Create();
            try
            {
                rsa.ImportSubjectPublicKeyInfo(der, out int read);
                if (read != der.Length || rsa.KeySize is < 2048 or > 4096 ||
                    Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()) != publicKey)
                    throw new CryptographicException("The selected mobile public key is not a canonical 2048-4096 bit RSA key.");
                return rsa;
            }
            catch { rsa.Dispose(); throw; }
        }

        internal static string EncryptDek(RSA mobile, byte[] dek)
        {
            if (dek.Length != 32)
                throw new CryptographicException("The registered desktop DEK must contain exactly 32 bytes.");
            return Convert.ToBase64String(mobile.Encrypt(dek, RSAEncryptionPadding.OaepSHA1));
        }
    }
}
