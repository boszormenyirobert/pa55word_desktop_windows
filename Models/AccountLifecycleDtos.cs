using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class AccountLifecycleRequestDto
    {
        [JsonPropertyName("accountId")]
        public string AccountId { get; init; } = string.Empty;

        [JsonPropertyName("desktopDeviceId")]
        public string DesktopDeviceId { get; init; } = string.Empty;

        [JsonPropertyName("nonce")]
        public string Nonce { get; init; } = string.Empty;
    }

    internal sealed class AccountStatusResponseDto
    {
        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;
    }
}