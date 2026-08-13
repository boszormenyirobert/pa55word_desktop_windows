using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class RootDeviceRegistrationResponseDto
    {
        [JsonPropertyName("dek")]
        public DesktopRegistration? Dek { get; set; }

        [JsonPropertyName("namespaces")]
        public List<NamespaceItemDto> Namespaces { get; set; } = new();
    }
}