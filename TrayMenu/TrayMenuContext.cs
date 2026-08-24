using TrayApp.Services;

namespace TrayApp.TrayMenu
{
    internal sealed class TrayMenuContext
    {
        public string StatusText { get; init; } = string.Empty;

        public string ActiveUserLabel { get; init; } = string.Empty;

        public string ActiveServerLabel { get; init; } = string.Empty;

        public string DeviceId { get; init; } = string.Empty;

        public string ConnectionSummary { get; init; } = string.Empty;

        public string LastUpdatedText { get; init; } = string.Empty;

        public int RegisteredUserCount { get; init; }

        public int RegisteredServerCount { get; init; }

        public IReadOnlyList<string> RegisteredUsers { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> RegisteredServers { get; init; } = Array.Empty<string>();

        public int CredentialGroupCount { get; init; }

        public int CredentialEntryCount { get; init; }

        public int FilteredCredentialEntryCount { get; init; }

        public string SelectedCredentialGroupLabel { get; init; } = string.Empty;

        public bool CredentialsLoading { get; init; }

        public string CredentialsErrorMessage { get; init; } = string.Empty;

        public AuthorizationSessionStore.AuthorizationSessionState? SessionState { get; init; }

        public bool HasDesktopSession => SessionState?.Active == true;
    }
}