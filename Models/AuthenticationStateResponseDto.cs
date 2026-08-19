using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class AuthenticationStateResponseDto
    {
        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("attestation")]
        public AuthenticationAttestationDto? Attestation { get; set; }
    }

    internal sealed class AuthenticationAttestationDto
    {
        [JsonPropertyName("attestationId")]
        public string? AttestationId { get; set; }

        [JsonPropertyName("accountId")]
        public string? AccountId { get; set; }

        [JsonPropertyName("desktopId")]
        public string? DesktopId { get; set; }

        [JsonPropertyName("expiresAt")]
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}