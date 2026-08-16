using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

/**
 *  after build
 *  powershell -ExecutionPolicy Bypass -File .\NativeMessaging\Install-NativeHost.ps1 -SourceDir "C:\Users\boszo\source\repos\TrayApp\bin\Debug\net8.0-windows" -Browser Edge
*/

namespace TrayApp.Services
{
    internal class HttpApiClient
    {
        private readonly HttpClient _httpClient;

        public HttpApiClient()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(3)
            };
        }


        public async Task ConnectToSseEndpoint(
            string endpoint,
            IReadOnlyDictionary<string, string>? headers,
            Action<string> onMessage,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("text/event-stream"));
            AddHeaders(request, headers);

            Debug.WriteLine($"[SSE] Sending GET request to '{endpoint}'.");

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            Debug.WriteLine(
                $"[SSE] Response headers received from '{endpoint}'. Status={(int)response.StatusCode} ({response.StatusCode}).");

            if (!response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

                throw new HttpRequestException(
                    $"GET {endpoint} failed with {(int)response.StatusCode} ({response.StatusCode}). Body: {responseBody}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(
                cancellationToken).ConfigureAwait(false);

            Debug.WriteLine($"[SSE] Response stream opened for '{endpoint}'.");

            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("data:"))
                {
                    string data = line.Substring(5).Trim();
                    onMessage(data);
                }
            }
        }

        public async Task PostJsonAsync<T>(
            string endpoint,
            T payload,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            string json = JsonSerializer.Serialize(payload);
            using var request = CreateJsonPostRequest(endpoint, json, headers);
            using var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"POST {endpoint} failed with {(int)response.StatusCode} ({response.StatusCode}). Body: {responseBody}");
            }
        }

        public async Task PutJsonAsync<T>(
            string endpoint,
            T payload,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            string json = JsonSerializer.Serialize(payload);
            using var request = CreateJsonPutRequest(endpoint, json, headers);
            using var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"PUT {endpoint} failed with {(int)response.StatusCode} ({response.StatusCode}). Body: {responseBody}");
            }
        }

        public async Task DeleteJsonAsync<T>(
            string endpoint,
            T payload,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            string json = JsonSerializer.Serialize(payload);
            using var request = DeleteJsonRequest(endpoint, json, headers);
            using var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"DELETE {endpoint} failed with {(int)response.StatusCode} ({response.StatusCode}). Body: {responseBody}");
            }
        }

        public async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(
            string endpoint,
            TRequest payload,
            IReadOnlyDictionary<string, string>? headers)
        {
            string json = JsonSerializer.Serialize(payload);
            using var request = CreateJsonPostRequest(endpoint, json, headers);
            using var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"POST {endpoint} failed with {(int)response.StatusCode} ({response.StatusCode}). Body: {responseBody}");
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return default;
            }

            return JsonSerializer.Deserialize<TResponse>(
                responseBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }

        public async Task<TResponse?> GetJsonAsync<TResponse>(
            string endpoint,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            AddHeaders(request, headers);

            Debug.WriteLine($"[HTTP-GET] Sending GET request to '{endpoint}'.");

            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            Debug.WriteLine(
                $"[HTTP-GET] Response received from '{endpoint}'. Status={(int)response.StatusCode} ({response.StatusCode}). Body: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"GET {endpoint} failed with {(int)response.StatusCode} ({response.StatusCode}). Body: {responseBody}");
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return default;
            }

            return JsonSerializer.Deserialize<TResponse>(
                responseBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }

        private static HttpRequestMessage CreateJsonPostRequest(
            string endpoint,
            string json,
            IReadOnlyDictionary<string, string>? headers)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            AddHeaders(request, headers);

            return request;
        }

        private static HttpRequestMessage CreateJsonPutRequest(
            string endpoint,
            string json,
            IReadOnlyDictionary<string, string>? headers)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            AddHeaders(request, headers);

            return request;
        }

        private static HttpRequestMessage DeleteJsonRequest(
            string endpoint,
            string json,
            IReadOnlyDictionary<string, string>? headers)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            AddHeaders(request, headers);

            return request;
        }
        
        private static void AddHeaders(
            HttpRequestMessage request,
            IReadOnlyDictionary<string, string>? headers)
        {
            if (headers == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    throw new InvalidOperationException($"Failed to add request header '{header.Key}'.");
                }
            }
        }
    }
}