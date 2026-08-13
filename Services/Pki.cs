using System.Buffers.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TrayApp.Services
{
    internal class Pki
    {
        private readonly CngProvider _provider =
            CngProvider.MicrosoftPlatformCryptoProvider;


        /// <summary>
        /// Checks whether the TPM key already exists.
        /// </summary>
        public bool PrivateKeyExists(string keyContainerName)
        {
            return CngKey.Exists(keyContainerName, _provider);
        }

        /// <summary>
        /// Creates a new TPM protected RSA-2048 private key.
        /// </summary>
        public void CreatePrivateKey(string keyContainerName)
        {
            if (PrivateKeyExists(keyContainerName)) return;

            var p = new CngKeyCreationParameters
            {
                Provider = _provider,
                ExportPolicy = CngExportPolicies.None,
                KeyUsage = CngKeyUsages.Signing | CngKeyUsages.Decryption
            };

            p.Parameters.Add(new CngProperty(
                "Length",
                BitConverter.GetBytes(2048),
                CngPropertyOptions.None));

            CngKey.Create(CngAlgorithm.Rsa, keyContainerName, p);
        }

        /// <summary>
        /// Returns the public key in PEM format.
        /// Used for config.json.
        /// </summary>
        public string GetPublicKeyPem(string keyContainerName)
        {
            using CngKey key =
                CngKey.Open(
                    keyContainerName,
                    _provider);

            using RSA rsa =
                new RSACng(key);

            return rsa.ExportSubjectPublicKeyInfoPem();
        }

        /// <summary>
        /// Returns the public key as DER Base64.
        /// Used inside the QR code payload.
        /// </summary>
        public string GetPublicKeyDerBase64(string keyContainerName)
        {
            using CngKey key =
                CngKey.Open(
                    keyContainerName,
                    _provider);

            using RSA rsa =
                new RSACng(key);

            byte[] der =
                rsa.ExportSubjectPublicKeyInfo();

            Debug.WriteLine($"Public key DER bytes: {der.Length}");

            return Convert.ToBase64String(der);
        }

        /// <summary>
        /// SHA-256 fingerprint of DER public key.
        /// Returns lowercase HEX string.
        /// </summary>
        public string GetFingerprint(string keyContainerName)
        {
            using CngKey key =
                CngKey.Open(
                    keyContainerName,
                    _provider);

            using RSA rsa =
                new RSACng(key);

            byte[] der =
                rsa.ExportSubjectPublicKeyInfo();

            byte[] hash =
                SHA256.HashData(der);

            return Convert
                .ToHexString(hash)
                .ToLowerInvariant();
        }

        /// <summary>
        /// Creates an RSA SHA256 signature.
        /// Returns Base64 encoded signature.
        /// </summary>
        public string Sign(
            string keyContainerName,
            string payload)
        {
            using CngKey key =
                CngKey.Open(
                    keyContainerName,
                    _provider);

            using RSA rsa =
                new RSACng(key);

            byte[] data =
                Encoding.UTF8.GetBytes(payload);

            byte[] sig =
                rsa.SignData(data, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);

            return Convert.ToBase64String(sig);
        }

        /// <summary>
        /// Decrypts a Base64 encoded RSA ciphertext with OAEP SHA-256.
        /// Returns raw plaintext bytes (DEK).
        /// </summary>
        public byte[] DecryptWithPrivateKeyOaepSha256(
            string keyContainerName,
            string encryptedBase64)
        {
            if (string.IsNullOrWhiteSpace(encryptedBase64))
            {
                throw new ArgumentException("Encrypted payload is empty.", nameof(encryptedBase64));
            }

            using CngKey key =
                CngKey.Open(
                    keyContainerName,
                    _provider);

            using RSA rsa =
                new RSACng(key);

            byte[] encryptedBytes =
                Convert.FromBase64String(encryptedBase64);

            return rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.OaepSHA1);
        }

        /// <summary>
        /// Deletes TPM key container.
        /// Useful for re-registration.
        /// </summary>
        public void DeletePrivateKey(string keyContainerName)
        {
            if (!PrivateKeyExists(keyContainerName))
            {
                return;
            }

            using CngKey key =
                CngKey.Open(
                    keyContainerName,
                    _provider);

            key.Delete();
        }
    }
}