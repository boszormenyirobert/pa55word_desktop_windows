namespace TrayApp.Services
{
    internal sealed class AuthorizationSessionStore
    {
        private readonly object _syncRoot = new();
        private DateTimeOffset? _authorizedUntilUtc;

        public bool IsAuthorized
        {
            get
            {
                lock (_syncRoot)
                {
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
            }
        }

        public void Revoke()
        {
            lock (_syncRoot)
            {
                _authorizedUntilUtc = null;
            }
        }
    }
}
