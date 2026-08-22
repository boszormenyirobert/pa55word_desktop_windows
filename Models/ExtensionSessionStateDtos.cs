using System.Text.Json.Serialization;

namespace TrayApp.Models
{
    internal sealed class ExtensionSessionStateResponseDto
    {
        [JsonPropertyName("authorized")]
        public bool Authorized { get; set; }

        [JsonPropertyName("closeExtension")]
        public bool CloseExtension { get; set; }

        [JsonPropertyName("authVersion")]
        public long AuthVersion { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    internal sealed class AuthorizationHealthResult
    {
        public bool Authorized { get; init; }
        public bool ExplicitlyInvalid { get; init; }
        public bool TransientFailure { get; init; }
        public string? Reason { get; init; }

        public static AuthorizationHealthResult Active() => new()
        {
            Authorized = true
        };

        public static AuthorizationHealthResult Invalid(string reason) => new()
        {
            ExplicitlyInvalid = true,
            Reason = reason
        };

        public static AuthorizationHealthResult Transient(string reason) => new()
        {
            TransientFailure = true,
            Reason = reason
        };
    }
}