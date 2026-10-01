using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal class ConnectionRequestDto
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("desktopDeviceId")]
        public string DesktopDeviceId { get; set; } = string.Empty;

        [JsonPropertyName("deviceName")]
        public string DeviceName { get; set; } = string.Empty;

        [JsonPropertyName("iv")]
        public string Iv { get; set; } = string.Empty;

        [JsonPropertyName("authTag")]
        public string AuthTag { get; set; } = string.Empty;

        [JsonPropertyName("registry")]
        public string Registry { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("groupId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? GroupId { get; set; }

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = string.Empty;
    }

    internal sealed class ConnectionDeleteRequestDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("desktopDeviceId")]
        public string DesktopDeviceId { get; set; } = string.Empty;

        [JsonPropertyName("deviceName")]
        public string DeviceName { get; set; } = string.Empty;

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; } = string.Empty;
    }
}
