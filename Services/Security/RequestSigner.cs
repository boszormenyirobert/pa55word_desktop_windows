using System.Collections.Generic;

namespace TrayApp.Services
{
    internal static class RequestSigner
    {
        public static IReadOnlyDictionary<string, string> CreateSignatureHeaders(
            string desktopDeviceId,
            string payloadJson,
            string operationName)
        {
            string keyContainerName = "KeyContainer_" + desktopDeviceId;
            string signature = new Pki().Sign(keyContainerName, payloadJson);

            if (string.IsNullOrWhiteSpace(signature))
            {
                throw new InvalidOperationException($"Failed to create signature for {operationName}.");
            }

            return new Dictionary<string, string>
            {
                ["x-signature"] = signature
            };
        }
    }
}