using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class AuthenticationResponseDto
    {
        [JsonPropertyName("accepted")]
        public bool? Accepted { get; set; }

        [JsonPropertyName("authorized")]
        public bool? Authorized { get; set; }

        [JsonPropertyName("authenticated")]
        public bool? Authenticated { get; set; }

        [JsonPropertyName("declined")]
        public bool? Declined { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("notificationId")]
        public string? NotificationId { get; set; }

        [JsonPropertyName("timeoutSeconds")]
        public int? TimeoutSeconds { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }
    }
}
