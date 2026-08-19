using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class AuthenticationRevokeResponseDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("deviceId")]
        public string? DeviceId { get; set; }
    }
}