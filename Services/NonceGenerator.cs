using System.Security.Cryptography;

namespace TrayApp.Services
{
    internal static class NonceGenerator
    {
        public static string Create()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        }
    }
}