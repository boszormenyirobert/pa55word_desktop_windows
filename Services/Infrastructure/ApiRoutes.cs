namespace TrayApp.Services
{
    internal static class ApiRoutes
    {
        public const string VersionPrefix = "/api/v1";
        public const string Authentication = "authentication";
        public const string AuthenticationStatus = "authentication/status";
        public const string Account = "account";
        public const string AccountStatus = "account/status";
        public const string CredentialStore = "credential-store";
        public const string CredentialStoreAll = "credential-store/all";
        public const string CredentialStoreGroup = "credential-store/group";
        public const string CredentialStoreGroupAll = "credential-store/group/all";

        public static string RootDevice(string desktopDeviceId)
        {
            return $"root-device/{Uri.EscapeDataString(desktopDeviceId)}";
        }

        public static string RootDevice(string desktopDeviceId, string nonce)
        {
            return $"{RootDevice(desktopDeviceId)}?nonce={Uri.EscapeDataString(nonce)}";
        }

        public static string AuthenticationStatusNotification(string accountId, string desktopDeviceId, string notificationId)
        {
            return $"{AuthenticationStatus}/{Uri.EscapeDataString(accountId)}/{Uri.EscapeDataString(desktopDeviceId)}/{Uri.EscapeDataString(notificationId)}";
        }

        public static string AuthenticationStatusNotification(string accountId, string desktopDeviceId, string notificationId, string nonce, string namespaceId)
        {
            return $"{AuthenticationStatusNotification(accountId, desktopDeviceId, notificationId)}/{Uri.EscapeDataString(namespaceId)}?nonce={Uri.EscapeDataString(nonce)}";
        }

        public static string AuthenticationState(string accountId, string desktopDeviceId, string nonce, string namespaceId)
        {
            return $"{Authentication}/{Uri.EscapeDataString(accountId)}/{Uri.EscapeDataString(desktopDeviceId)}/{Uri.EscapeDataString(namespaceId)}?nonce={Uri.EscapeDataString(nonce)}";
        }

        public static string AuthenticationRevoke(string accountId, string desktopDeviceId, string nonce, string namespaceId)
        {
            return AuthenticationRevoke(accountId, desktopDeviceId, nonce);
        }

        public static string AuthenticationRevoke(string accountId, string desktopDeviceId, string nonce)
        {
            return $"{Authentication}/{Uri.EscapeDataString(accountId)}/{Uri.EscapeDataString(desktopDeviceId)}?nonce={Uri.EscapeDataString(nonce)}";
        }

        public static string ResolveNamespace(string desktopDeviceId)
        {
            return $"account/resolve/{Uri.EscapeDataString(desktopDeviceId)}";
        }

        public static string ResolveNamespace(string desktopDeviceId, string nonce)
        {
            return $"{ResolveNamespace(desktopDeviceId)}?nonce={Uri.EscapeDataString(nonce)}";
        }
    }
}