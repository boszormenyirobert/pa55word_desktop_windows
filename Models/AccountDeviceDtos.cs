using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class AccountDevicesRequestDto
    {
        [JsonPropertyName("accountId")]
        public string AccountId { get; init; } = string.Empty;

        [JsonPropertyName("desktopDeviceId")]
        public string DesktopDeviceId { get; init; } = string.Empty;

        [JsonPropertyName("nonce")]
        public string Nonce { get; init; } = string.Empty;
    }

    internal sealed class AccountDeviceDeleteRequestDto
    {
        [JsonPropertyName("accountId")]
        public string AccountId { get; init; } = string.Empty;

        [JsonPropertyName("desktopDeviceId")]
        public string DesktopDeviceId { get; init; } = string.Empty;

        [JsonPropertyName("targetMobileDeviceId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TargetMobileDeviceId { get; init; }

        [JsonPropertyName("targetDesktopDeviceId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TargetDesktopDeviceId { get; init; }

        [JsonPropertyName("nonce")]
        public string Nonce { get; init; } = string.Empty;
    }

    internal sealed class AccountDevicesResponseDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("devices")]
        public List<AccountDeviceDto> Devices { get; init; } = new();
    }

    internal sealed class AccountDeviceDeleteResponseDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    internal sealed class AccountDeviceDto
    {
        [JsonPropertyName("mobileDeviceId")]
        public string? MobileDeviceId { get; init; }

        [JsonPropertyName("desktopDeviceId")]
        public string? DesktopDeviceId { get; init; }

        [JsonPropertyName("deviceName")]
        public string? DeviceName { get; init; }

        [JsonPropertyName("lastTimeUsed")]
        public DateTimeOffset? LastTimeUsed { get; init; }

        [JsonPropertyName("namespaces")]
        public List<AccountDeviceNamespaceDto> Namespaces { get; init; } = new();
    }

    internal sealed class AccountDeviceNamespaceDto
    {
        [JsonPropertyName("namespaceId")]
        public string NamespaceId { get; init; } = string.Empty;

        [JsonPropertyName("namespace")]
        public string Namespace { get; init; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;

        [JsonPropertyName("root")]
        public bool Root { get; init; }
    }
}
