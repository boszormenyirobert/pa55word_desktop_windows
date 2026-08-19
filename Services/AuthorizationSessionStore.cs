using System.Text.Json;

namespace TrayApp.Services
{
    internal sealed class AuthorizationSessionStore
    {
        private sealed class AuthorizationSessionSnapshot
        {
            public bool Active { get; set; }
            public string BoundUserId { get; set; } = string.Empty;
            public string AccountId { get; set; } = string.Empty;
            public string DesktopId { get; set; } = string.Empty;
            public DateTimeOffset? ExpiresAtUtc { get; set; }
            public string? AttestationId { get; set; }
        }

        internal sealed class AuthorizationSessionState
        {
            public bool Active { get; init; }
            public string BoundUserId { get; init; } = string.Empty;
            public string AccountId { get; init; } = string.Empty;
            public string DesktopId { get; init; } = string.Empty;
            public DateTimeOffset? ExpiresAtUtc { get; init; }
            public string? AttestationId { get; init; }
        }

        private readonly object _syncRoot = new();
        private readonly string _filePath;
        private AuthorizationSessionSnapshot? _snapshot;

        public AuthorizationSessionStore()
        {
            string sessionDirectoryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TrayApp");

            Directory.CreateDirectory(sessionDirectoryPath);
            _filePath = Path.Combine(sessionDirectoryPath, "authorization-session.json");
            _snapshot = LoadSnapshot();
        }

        public bool IsAuthorized
        {
            get
            {
                lock (_syncRoot)
                {
                    if (!TryGetSnapshotCore(out AuthorizationSessionSnapshot? snapshot))
                    {
                        return false;
                    }

                    AuthorizationSessionSnapshot currentSnapshot = snapshot!;

                    string? activeUserId = AppEnvironment.GetActiveUserId();
                    return !string.IsNullOrWhiteSpace(activeUserId) &&
                           string.Equals(currentSnapshot.BoundUserId, activeUserId, StringComparison.Ordinal);
                }
            }
        }

        public bool Matches(string boundUserId, string accountId, string desktopId)
        {
            lock (_syncRoot)
            {
                if (!TryGetSnapshotCore(out AuthorizationSessionSnapshot? snapshot))
                {
                    return false;
                }

                AuthorizationSessionSnapshot currentSnapshot = snapshot!;

                return string.Equals(currentSnapshot.BoundUserId, boundUserId, StringComparison.Ordinal) &&
                       string.Equals(currentSnapshot.AccountId, accountId, StringComparison.Ordinal) &&
                       string.Equals(currentSnapshot.DesktopId, desktopId, StringComparison.Ordinal);
            }
        }

        public bool TryGetState(out AuthorizationSessionState? state)
        {
            lock (_syncRoot)
            {
                if (!TryGetSnapshotCore(out AuthorizationSessionSnapshot? snapshot))
                {
                    state = null;
                    return false;
                }

                AuthorizationSessionSnapshot currentSnapshot = snapshot!;

                state = new AuthorizationSessionState
                {
                    Active = currentSnapshot.Active,
                    BoundUserId = currentSnapshot.BoundUserId,
                    AccountId = currentSnapshot.AccountId,
                    DesktopId = currentSnapshot.DesktopId,
                    ExpiresAtUtc = currentSnapshot.ExpiresAtUtc,
                    AttestationId = currentSnapshot.AttestationId
                };

                return true;
            }
        }

        public void SaveActive(
            string boundUserId,
            string accountId,
            string desktopId,
            DateTimeOffset? expiresAtUtc,
            string? attestationId)
        {
            lock (_syncRoot)
            {
                _snapshot = new AuthorizationSessionSnapshot
                {
                    Active = true,
                    BoundUserId = boundUserId,
                    AccountId = accountId,
                    DesktopId = desktopId,
                    ExpiresAtUtc = expiresAtUtc,
                    AttestationId = attestationId
                };

                SaveAuthorizationSession();
            }
        }

        public void Revoke()
        {
            lock (_syncRoot)
            {
                ClearAuthorizationSession();
            }
        }

        private AuthorizationSessionSnapshot? LoadSnapshot()
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                AuthorizationSessionSnapshot? snapshot = JsonSerializer.Deserialize<AuthorizationSessionSnapshot>(json);

                if (snapshot == null ||
                    !snapshot.Active ||
                    string.IsNullOrWhiteSpace(snapshot.BoundUserId) ||
                    string.IsNullOrWhiteSpace(snapshot.AccountId) ||
                    string.IsNullOrWhiteSpace(snapshot.DesktopId))
                {
                    return null;
                }

                if (snapshot.ExpiresAtUtc.HasValue && snapshot.ExpiresAtUtc.Value <= DateTimeOffset.UtcNow)
                {
                    return null;
                }

                return snapshot;
            }
            catch
            {
                return null;
            }
        }

        private void SaveAuthorizationSession()
        {
            string json = JsonSerializer.Serialize(_snapshot);
            File.WriteAllText(_filePath, json);
        }

        private bool TryGetSnapshotCore(out AuthorizationSessionSnapshot? snapshot)
        {
            snapshot = _snapshot;

            if (snapshot == null ||
                !snapshot.Active ||
                string.IsNullOrWhiteSpace(snapshot.BoundUserId) ||
                string.IsNullOrWhiteSpace(snapshot.AccountId) ||
                string.IsNullOrWhiteSpace(snapshot.DesktopId))
            {
                snapshot = null;
                return false;
            }

            if (snapshot.ExpiresAtUtc.HasValue && snapshot.ExpiresAtUtc.Value <= DateTimeOffset.UtcNow)
            {
                ClearAuthorizationSession();
                snapshot = null;
                return false;
            }

            return true;
        }

        private void ClearAuthorizationSession()
        {
            _snapshot = null;

            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
    }
}
