using System.Text.Json;

namespace TrayApp.Services
{
    internal sealed class AuthorizationSessionStore
    {
        private sealed class AuthorizationSessionSnapshot
        {
            public DateTimeOffset? AuthorizedUntilUtc { get; set; }
        }

        private readonly object _syncRoot = new();
        private readonly string _filePath;
        private DateTimeOffset? _authorizedUntilUtc;

        public AuthorizationSessionStore()
        {
            string sessionDirectoryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TrayApp");

            Directory.CreateDirectory(sessionDirectoryPath);
            _filePath = Path.Combine(sessionDirectoryPath, "authorization-session.json");
            _authorizedUntilUtc = LoadAuthorizedUntilUtc();
        }

        public bool IsAuthorized
        {
            get
            {
                lock (_syncRoot)
                {
                    if (_authorizedUntilUtc.HasValue && _authorizedUntilUtc.Value <= DateTimeOffset.UtcNow)
                    {
                        ClearAuthorizationSession();
                        return false;
                    }

                    return _authorizedUntilUtc.HasValue &&
                           _authorizedUntilUtc.Value > DateTimeOffset.UtcNow;
                }
            }
        }

        public void AuthorizeFor(TimeSpan duration)
        {
            lock (_syncRoot)
            {
                _authorizedUntilUtc = DateTimeOffset.UtcNow.Add(duration);
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

        private DateTimeOffset? LoadAuthorizedUntilUtc()
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                AuthorizationSessionSnapshot? snapshot = JsonSerializer.Deserialize<AuthorizationSessionSnapshot>(json);

                if (snapshot?.AuthorizedUntilUtc is not DateTimeOffset authorizedUntilUtc)
                {
                    return null;
                }

                return authorizedUntilUtc;
            }
            catch
            {
                return null;
            }
        }

        private void SaveAuthorizationSession()
        {
            var snapshot = new AuthorizationSessionSnapshot
            {
                AuthorizedUntilUtc = _authorizedUntilUtc
            };

            string json = JsonSerializer.Serialize(snapshot);
            File.WriteAllText(_filePath, json);
        }

        private void ClearAuthorizationSession()
        {
            _authorizedUntilUtc = null;

            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
    }
}
