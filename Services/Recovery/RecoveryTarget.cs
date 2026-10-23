using TrayApp.Models;

namespace TrayApp.Services
{
    internal sealed record RecoveryTarget(
        string UserId, string ProfileName, string NamespaceId, string ServerName,
        string BaseUrl, string AccountId, string DesktopDeviceId, string EncDek)
    {
        public static RecoveryTarget From(DesktopRegistration registration, NamespaceItemDto server)
        {
            string origin = NormalizeOrigin(server.Url);
            if (string.IsNullOrWhiteSpace(server.AccountId) ||
                string.IsNullOrWhiteSpace(server.DesktopDeviceId) ||
                string.IsNullOrWhiteSpace(server.EncDek))
                throw new InvalidDataException("This database registration has no account, desktop identity or encrypted DEK. Synchronize the database registration first.");
            return new RecoveryTarget(registration.UserKey, registration.DisplayUserName, server.NamespaceId,
                server.Namespace, origin, server.AccountId, server.DesktopDeviceId, server.EncDek);
        }

        public static string NormalizeOrigin(string url)
        {
            // HTTP is allowed for development only; disable it in production and require HTTPS.
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                uri.AbsolutePath != "/")
                throw new InvalidDataException("Recovery requires a registered HTTP or HTTPS server origin without a path, credentials, query or fragment.");
            return uri.GetLeftPart(UriPartial.Authority);
        }

        public string StorageKey => string.Join("\n", UserId, BaseUrl, AccountId, DesktopDeviceId);
    }
}
