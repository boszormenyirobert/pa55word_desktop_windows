using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class AuthenticationRequestDto
    {
        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("desktopDeviceId")]
        public string DesktopDeviceId { get; set; } = string.Empty;

        [JsonIgnore]
        public string DeviceId
        {
            get => DesktopDeviceId;
            set => DesktopDeviceId = value;
        }

        [JsonPropertyName("namespaceId")]
        public string NamespaceId { get; set; } = string.Empty;

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = string.Empty;
    }
}
