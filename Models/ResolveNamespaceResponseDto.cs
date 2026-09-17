using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class ResolveNamespaceResponseDto
    {
        [JsonPropertyName("namespaceId")]
        public string NamespaceId { get; set; } = string.Empty;

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("namespaceName")]
        public string NamespaceName { get; set; } = string.Empty;
    }
}