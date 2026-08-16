using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class ResolveNamespaceResponseDto
    {
        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;
    }
}