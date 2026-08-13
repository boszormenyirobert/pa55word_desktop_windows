using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal class ConnectionReadRequestDto
    {
        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = string.Empty;
    }
}
