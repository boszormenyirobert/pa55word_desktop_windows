namespace TrayApp.Services
{
    internal static class CredentialApiEndpoints
    {
        public static string Connection => AppEnvironment.BuildApiUrl(ApiRoutes.CredentialStore);

        public static string ConnectionRead => AppEnvironment.BuildApiUrl(ApiRoutes.CredentialStoreAll);

        public static string Group => AppEnvironment.BuildApiUrl(ApiRoutes.CredentialStoreGroup);

        public static string GroupRead => AppEnvironment.BuildApiUrl(ApiRoutes.CredentialStoreGroupAll);
    }
}