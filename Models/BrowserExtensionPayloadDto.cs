using System.Text.Json;

namespace TrayApp.Models;

internal class BrowserExtensionPayloadDto
{
    public JsonElement? Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? GroupId { get; set; }
    public string? GroupName { get; set; }
    public string? Iv { get; set; }
    public string? AuthTag { get; set; }
    public string? EncryptedRegistry { get; set; }
    public string? Resource { get; set; }
    public BrowserRegistryDto Registry { get; set; } = new BrowserRegistryDto();
    public string Rest { get; set; } = string.Empty;
}

internal class BrowserRegistryDto
{
    public string Domain { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
