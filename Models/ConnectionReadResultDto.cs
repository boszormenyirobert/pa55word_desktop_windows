using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal class ConnectionReadResultDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("credentialGroupId")]
        public int? CredentialGroupId { get; set; }

        [JsonPropertyName("registry")]
        public string Registry { get; set; } = string.Empty;

        [JsonPropertyName("group")]
        public CredentialGroupReferenceDto? Group { get; set; }
    }
}
