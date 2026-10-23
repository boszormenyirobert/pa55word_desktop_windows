using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal class RecoveryStartedDto
    {
        [JsonPropertyName("recoveryId")]
        public string RecoveryId { get; set; } = string.Empty;
    }

    internal class RecoveryStateDto : RecoveryStartedDto
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("expiresAt")]
        public DateTimeOffset ExpiresAt { get; set; }
    }

    internal sealed class RecoverySelectedDto : RecoveryStateDto
    {
        [JsonPropertyName("candidateId")]
        public string CandidateId { get; set; } = string.Empty;
        [JsonPropertyName("mobileDeviceId")]
        public string MobileDeviceId { get; set; } = string.Empty;
        [JsonPropertyName("publicKey")]
        public string PublicKey { get; set; } = string.Empty;
        [JsonPropertyName("deviceName")]
        public string DeviceName { get; set; } = string.Empty;
    }
}
