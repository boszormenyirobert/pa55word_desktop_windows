namespace TrayApp.Services
{
    internal static class ApiRoutes
    {
        public const string VersionPrefix = "/api/v1";
        public const string Authentication = "authentication";
        public const string AuthenticationStatus = "authentication/status";
        public const string Namespace = "namespace";
        public const string CredentialStore = "credential-store";
        public const string CredentialStoreAll = "credential-store/all";
        public const string CredentialStoreGroup = "credential-store/group";
        public const string CredentialStoreGroupAll = "credential-store/group/all";

        public static string RootDevice(string deviceId)
        {
            return $"root-device/{Uri.EscapeDataString(deviceId)}";
        }

        public static string AuthenticationStatusNotification(string accountId, string deviceId, string notificationId)
        {
            return $"{AuthenticationStatus}/{Uri.EscapeDataString(accountId)}/{Uri.EscapeDataString(deviceId)}/{Uri.EscapeDataString(notificationId)}";
        }

        public static string AuthenticationStatusNotification(string accountId, string deviceId, string notificationId, string nonce)
        {
            return $"{AuthenticationStatusNotification(accountId, deviceId, notificationId)}?nonce={Uri.EscapeDataString(nonce)}";
        }

        public static string AuthenticationState(string accountId, string deviceId, string nonce)
        {
            return $"{Authentication}/{Uri.EscapeDataString(accountId)}/{Uri.EscapeDataString(deviceId)}?nonce={Uri.EscapeDataString(nonce)}";
        }

        public static string AuthenticationRevoke(string accountId, string deviceId, string nonce)
        {
            return AuthenticationState(accountId, deviceId, nonce);
        }

        public static string NamespacesForDevice(string accountId, string deviceId)
        {
            return $"{Namespace}/{Uri.EscapeDataString(accountId)}/{Uri.EscapeDataString(deviceId)}";
        }

        public static string ResolveNamespace(string deviceId, string namespaceId)
        {
            return $"account/resolve/{Uri.EscapeDataString(deviceId)}/{Uri.EscapeDataString(namespaceId)}";
        }
    }
}