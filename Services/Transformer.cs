using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TrayApp.Models;


namespace TrayApp.Services
{
    internal class Transformer
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static DesktopRegistration? ToRegistrationResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            RootDeviceRegistrationResponseDto? wrappedResponse = JsonSerializer.Deserialize<RootDeviceRegistrationResponseDto>(
                json,
                JsonOptions);

            if (wrappedResponse?.Dek != null)
            {
                wrappedResponse.Dek.Namespaces = wrappedResponse.Namespaces ?? new List<NamespaceItemDto>();
                return wrappedResponse.Dek;
            }

            return JsonSerializer.Deserialize<DesktopRegistration>(
                json,
                JsonOptions);
        }
    }
}
