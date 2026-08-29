using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal class ConnectionReadItemDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("desktopDeviceId")]
        public string DesktopDeviceId { get; set; } = string.Empty;

        [JsonPropertyName("credentialGroupId")]
        public int? CredentialGroupId { get; set; }

        [JsonPropertyName("iv")]
        public string Iv { get; set; } = string.Empty;

        [JsonPropertyName("authTag")]
        public string AuthTag { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("registry")]
        public string Registry { get; set; } = string.Empty;

        [JsonPropertyName("group")]
        public CredentialGroupReferenceDto? Group { get; set; }
    }
}
