using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed class RecoverySession
    {
        public string TargetKey { get; set; } = string.Empty;
        public string RecoveryId { get; set; } = string.Empty;
        public string Status { get; set; } = "not_started";
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset? RetryAt { get; set; }
        public DateTimeOffset? EmailResendAt { get; set; }
        public RecoverySelectedDto? Selected { get; set; }
        public string? PendingJson { get; set; }
        public string? PendingOperation { get; set; }
    }

    internal interface IRecoverySessionStore
    {
        RecoverySession? Load(RecoveryTarget target);
        void Save(RecoveryTarget target, RecoverySession session);
        void Delete(RecoveryTarget target);
    }

    internal sealed class RecoverySessionStore : IRecoverySessionStore
    {
        private readonly string _directory;

        public RecoverySessionStore(string? directory = null)
        {
            _directory = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrayApp", "recovery");
        }

        private string FilePath(RecoveryTarget target) => Path.Combine(_directory,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.StorageKey))) + ".bin");

        public RecoverySession? Load(RecoveryTarget target)
        {
            string path = FilePath(target);
            if (!File.Exists(path)) return null;
            byte[] plaintext = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            try
            {
                var session = JsonSerializer.Deserialize<RecoverySession>(plaintext)
                    ?? throw new InvalidDataException("Saved recovery state is empty.");
                if (session.TargetKey != target.StorageKey)
                    throw new InvalidDataException("Saved recovery state belongs to another registration.");
                return session;
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }

        public void Save(RecoveryTarget target, RecoverySession session)
        {
            Directory.CreateDirectory(_directory);
            session.TargetKey = target.StorageKey;
            byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(session);
            try
            {
                byte[] encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
                string path = FilePath(target), temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        file.Write(encrypted);
                        file.Flush(flushToDisk: true);
                    }
                    File.Move(temporary, path, overwrite: true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }

        public void Delete(RecoveryTarget target) => File.Delete(FilePath(target));
    }
}
