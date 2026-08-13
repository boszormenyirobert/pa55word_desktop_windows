using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class DesktopRegistration
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonPropertyName("encDek")]
        public string EncDek { get; set; } = string.Empty;

        [JsonPropertyName("namespaces")]
        public List<NamespaceItemDto> Namespaces { get; set; } = new();
    }
}
