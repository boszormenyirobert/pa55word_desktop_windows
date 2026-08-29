using System.Text.Json;

namespace TrayApp.Services
{
    internal sealed class AuthorizationSessionStore
    {
        private sealed class AuthorizationSessionSnapshot
        {
            public bool Active { get; set; }
            public string BoundUserId { get; set; } = string.Empty;
            public string NamespaceId { get; set; } = string.Empty;
            public string BaseUrl { get; set; } = string.Empty;
            public string AccountId { get; set; } = string.Empty;
            public string DesktopId { get; set; } = string.Empty;
            public DateTimeOffset? ExpiresAtUtc { get; set; }
            public string? AttestationId { get; set; }
        }

        internal sealed class AuthorizationSessionState
        {
            public bool Active { get; init; }
            public string BoundUserId { get; init; } = string.Empty;
            public string NamespaceId { get; init; } = string.Empty;
            public string BaseUrl { get; init; } = string.Empty;
            public string AccountId { get; init; } = string.Empty;
            public string DesktopId { get; init; } = string.Empty;
            public DateTimeOffset? ExpiresAtUtc { get; init; }
            public string? AttestationId { get; init; }
        }

        private readonly object _syncRoot = new();
        private readonly string _filePath;
        private List<AuthorizationSessionSnapshot> _snapshots;

        public AuthorizationSessionStore()
        {
            string sessionDirectoryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TrayApp");

            Directory.CreateDirectory(sessionDirectoryPath);
            _filePath = Path.Combine(sessionDirectoryPath, "authorization-session.json");
            _snapshots = LoadSnapshots();
        }

        public bool IsAuthorized
        {
            get
            {
                lock (_syncRoot)
                {
                    string? activeUserId = AppEnvironment.GetActiveUserId();
                    return !string.IsNullOrWhiteSpace(activeUserId) &&
                           GetActiveSnapshotsCore().Any(snapshot =>
                               string.Equals(snapshot.BoundUserId, activeUserId, StringComparison.Ordinal));
                }
            }
        }

        public bool Matches(string boundUserId, string accountId, string desktopId, string baseUrl)
        {
            lock (_syncRoot)
            {
                return GetActiveSnapshotsCore().Any(snapshot =>
                    string.Equals(snapshot.BoundUserId, boundUserId, StringComparison.Ordinal) &&
                    string.Equals(snapshot.BaseUrl, NormalizeBaseUrl(baseUrl), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(snapshot.AccountId, accountId, StringComparison.Ordinal) &&
                    string.Equals(snapshot.DesktopId, desktopId, StringComparison.Ordinal));
            }
        }

        public bool HasActiveSessionForNamespace(string boundUserId, string namespaceId)
        {
            if (string.IsNullOrWhiteSpace(boundUserId) || string.IsNullOrWhiteSpace(namespaceId))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return GetActiveSnapshotsCore().Any(snapshot =>
                    string.Equals(snapshot.BoundUserId, boundUserId, StringComparison.Ordinal) &&
                    string.Equals(snapshot.NamespaceId, namespaceId, StringComparison.Ordinal));
            }
        }

        public bool HasActiveSessionForBaseUrl(string boundUserId, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(boundUserId) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return false;
            }

            lock (_syncRoot)
            {
                string normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
                return GetActiveSnapshotsCore().Any(snapshot =>
                    string.Equals(snapshot.BoundUserId, boundUserId, StringComparison.Ordinal) &&
                    string.Equals(snapshot.BaseUrl, normalizedBaseUrl, StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool TryGetState(out AuthorizationSessionState? state)
        {
            lock (_syncRoot)
            {
                string? activeUserId = AppEnvironment.GetActiveUserId();
                if (string.IsNullOrWhiteSpace(activeUserId))
                {
                    state = null;
                    return false;
                }

                AuthorizationSessionSnapshot? snapshot = GetActiveSnapshotsCore().FirstOrDefault(item =>
                    string.Equals(item.BoundUserId, activeUserId, StringComparison.Ordinal));

                if (snapshot == null)
                {
                    state = null;
                    return false;
                }

                state = new AuthorizationSessionState
                {
                    Active = snapshot.Active,
                    BoundUserId = snapshot.BoundUserId,
                    NamespaceId = snapshot.NamespaceId,
                    BaseUrl = snapshot.BaseUrl,
                    AccountId = snapshot.AccountId,
                    DesktopId = snapshot.DesktopId,
                    ExpiresAtUtc = snapshot.ExpiresAtUtc,
                    AttestationId = snapshot.AttestationId
                };

                return true;
            }
        }

        public IReadOnlyList<AuthorizationSessionState> GetStates(string boundUserId)
        {
            if (string.IsNullOrWhiteSpace(boundUserId))
            {
                return Array.Empty<AuthorizationSessionState>();
            }

            lock (_syncRoot)
            {
                return GetActiveSnapshotsCore()
                    .Where(snapshot => string.Equals(snapshot.BoundUserId, boundUserId, StringComparison.Ordinal))
                    .Select(snapshot => new AuthorizationSessionState
                    {
                        Active = snapshot.Active,
                        BoundUserId = snapshot.BoundUserId,
                        NamespaceId = snapshot.NamespaceId,
                        BaseUrl = snapshot.BaseUrl,
                        AccountId = snapshot.AccountId,
                        DesktopId = snapshot.DesktopId,
                        ExpiresAtUtc = snapshot.ExpiresAtUtc,
                        AttestationId = snapshot.AttestationId
                    })
                    .ToArray();
            }
        }

        public void SaveActive(
            string boundUserId,
            string namespaceId,
            string baseUrl,
            string accountId,
            string desktopId,
            DateTimeOffset? expiresAtUtc,
            string? attestationId)
        {
            lock (_syncRoot)
            {
                string normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
                AuthorizationSessionSnapshot? existingSnapshot = GetActiveSnapshotsCore().FirstOrDefault(snapshot =>
                    string.Equals(snapshot.BoundUserId, boundUserId, StringComparison.Ordinal) &&
                    string.Equals(snapshot.NamespaceId, namespaceId, StringComparison.Ordinal) &&
                    string.Equals(snapshot.BaseUrl, normalizedBaseUrl, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(snapshot.AccountId, accountId, StringComparison.Ordinal) &&
                    string.Equals(snapshot.DesktopId, desktopId, StringComparison.Ordinal));

                if (existingSnapshot == null)
                {
                    _snapshots.Add(new AuthorizationSessionSnapshot
                    {
                        Active = true,
                        BoundUserId = boundUserId,
                        NamespaceId = namespaceId,
                        BaseUrl = normalizedBaseUrl,
                        AccountId = accountId,
                        DesktopId = desktopId,
                        ExpiresAtUtc = expiresAtUtc,
                        AttestationId = attestationId
                    });
                }
                else
                {
                    existingSnapshot.Active = true;
                    existingSnapshot.ExpiresAtUtc = expiresAtUtc;
                    existingSnapshot.AttestationId = attestationId;
                }

                SaveAuthorizationSession();
            }
        }

        public void Revoke(string boundUserId, string accountId, string desktopId, string baseUrl)
        {
            lock (_syncRoot)
            {
            string normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
                if (_snapshots.RemoveAll(snapshot =>
                        string.Equals(snapshot.BoundUserId, boundUserId, StringComparison.Ordinal) &&
                string.Equals(snapshot.BaseUrl, normalizedBaseUrl, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(snapshot.AccountId, accountId, StringComparison.Ordinal) &&
                        string.Equals(snapshot.DesktopId, desktopId, StringComparison.Ordinal)) > 0)
                {
                    SaveAuthorizationSession();
                }
            }
        }

        public void Revoke(string boundUserId, string namespaceId)
        {
            if (string.IsNullOrWhiteSpace(boundUserId) || string.IsNullOrWhiteSpace(namespaceId))
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_snapshots.RemoveAll(snapshot =>
                        string.Equals(snapshot.BoundUserId, boundUserId, StringComparison.Ordinal) &&
                        string.Equals(snapshot.NamespaceId, namespaceId, StringComparison.Ordinal)) > 0)
                {
                    SaveAuthorizationSession();
                }
            }
        }

        public void Revoke()
        {
            lock (_syncRoot)
            {
                ClearAuthorizationSessions();
            }
        }

        private List<AuthorizationSessionSnapshot> LoadSnapshots()
        {
            if (!File.Exists(_filePath))
            {
                return new List<AuthorizationSessionSnapshot>();
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                List<AuthorizationSessionSnapshot>? snapshots = JsonSerializer.Deserialize<List<AuthorizationSessionSnapshot>>(json);
                if (snapshots != null)
                {
                    return snapshots.Where(IsValidSnapshot).ToList();
                }

                AuthorizationSessionSnapshot? snapshot = JsonSerializer.Deserialize<AuthorizationSessionSnapshot>(json);
                if (snapshot != null && IsValidSnapshot(snapshot))
                {
                    return new List<AuthorizationSessionSnapshot> { snapshot };
                }

                return new List<AuthorizationSessionSnapshot>();
            }
            catch
            {
                return new List<AuthorizationSessionSnapshot>();
            }
        }

        private void SaveAuthorizationSession()
        {
            List<AuthorizationSessionSnapshot> activeSnapshots = GetActiveSnapshotsCore();

            if (activeSnapshots.Count == 0)
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }

                return;
            }

            string json = JsonSerializer.Serialize(activeSnapshots);
            File.WriteAllText(_filePath, json);
        }

        private List<AuthorizationSessionSnapshot> GetActiveSnapshotsCore()
        {
            bool removedExpired = _snapshots.RemoveAll(snapshot => !IsValidSnapshot(snapshot)) > 0;

            if (removedExpired)
            {
                SaveAuthorizationSession();
            }

            return _snapshots;
        }

        private void ClearAuthorizationSessions()
        {
            _snapshots.Clear();

            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        private static bool IsValidSnapshot(AuthorizationSessionSnapshot snapshot)
        {
            if (!snapshot.Active ||
                string.IsNullOrWhiteSpace(snapshot.BoundUserId) ||
                string.IsNullOrWhiteSpace(snapshot.BaseUrl) ||
                string.IsNullOrWhiteSpace(snapshot.AccountId) ||
                string.IsNullOrWhiteSpace(snapshot.DesktopId))
            {
                return false;
            }

            return !snapshot.ExpiresAtUtc.HasValue || snapshot.ExpiresAtUtc.Value > DateTimeOffset.UtcNow;
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            return string.IsNullOrWhiteSpace(baseUrl)
                ? string.Empty
                : baseUrl.TrimEnd('/');
        }
    }
}
