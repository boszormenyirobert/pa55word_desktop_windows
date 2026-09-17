using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class AuthenticationStatusRequestDto
    {
        [JsonPropertyName("namespaceId")]
        public string NamespaceId { get; set; } = string.Empty;

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("desktopDeviceId")]
        public string DesktopDeviceId { get; set; } = string.Empty;

        [JsonPropertyName("notificationId")]
        public string NotificationId { get; set; } = string.Empty;

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = string.Empty;
    }
}
