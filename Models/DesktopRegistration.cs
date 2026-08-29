using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class DesktopRegistration
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("useName")]
        public string LegacyUserName
        {
            set
            {
                if (string.IsNullOrWhiteSpace(UserName))
                {
                    UserName = value ?? string.Empty;
                }
            }
        }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("desktopDeviceId")]
        public string DesktopDeviceId { get; set; } = string.Empty;

        [JsonPropertyName("encDek")]
        public string EncDek { get; set; } = string.Empty;

        [JsonPropertyName("namespaces")]
        public List<NamespaceItemDto> Namespaces { get; set; } = new();

        [JsonIgnore]
        public string UserKey => string.IsNullOrWhiteSpace(UserId)
            ? DesktopDeviceId
            : UserId;

        [JsonIgnore]
        public string DisplayUserName => string.IsNullOrWhiteSpace(UserName)
            ? (string.IsNullOrWhiteSpace(DesktopDeviceId) ? "Unnamed user" : DesktopDeviceId)
            : UserName;
    }
}
