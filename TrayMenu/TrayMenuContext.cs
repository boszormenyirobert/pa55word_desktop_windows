using TrayApp.Services;

namespace TrayApp.TrayMenu
{
    internal sealed class RegisteredServerDetail
    {
        public string UserId { get; init; } = string.Empty;

        public string NamespaceId { get; init; } = string.Empty;

        public string UserName { get; init; } = string.Empty;

        public string DeviceName { get; init; } = string.Empty;

        public string Namespace { get; init; } = string.Empty;

        public string Url { get; init; } = string.Empty;

        public bool HasSession { get; init; }
    }

    internal sealed class TrayMenuContext
    {
        public string StatusText { get; init; } = string.Empty;

        public string ActiveUserLabel { get; init; } = string.Empty;

        public string ActiveServerLabel { get; init; } = string.Empty;

        public string DesktopDeviceId { get; init; } = string.Empty;

        public string DeviceId => DesktopDeviceId;

        public string ConnectionSummary { get; init; } = string.Empty;

        public string LastUpdatedText { get; init; } = string.Empty;

        public int RegisteredUserCount { get; init; }

        public int RegisteredServerCount { get; init; }

        public IReadOnlyList<string> RegisteredUsers { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> RegisteredServers { get; init; } = Array.Empty<string>();

        public IReadOnlyList<RegisteredServerDetail> RegisteredServerDetails { get; init; } = Array.Empty<RegisteredServerDetail>();

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