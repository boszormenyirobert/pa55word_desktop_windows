using System.Net.Http.Headers;
using System.Diagnostics;
using System.Net;
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

            LogRequest(request, payloadJson: null, "[SSE]");

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            LogResponse(response, endpoint, responseBody: null, "[SSE]");

            if (!response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

                throw new ApiRequestException(
                    HttpMethod.Get,
                    endpoint,
                    response.StatusCode,
                    responseBody);
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
            LogRequest(request, json, "[HTTP-POST]");
            using var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();
            LogResponse(response, endpoint, responseBody, "[HTTP-POST]");

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiRequestException(
                    HttpMethod.Post,
                    endpoint,
                    response.StatusCode,
                    responseBody);
            }
        }

        public async Task PutJsonAsync<T>(
            string endpoint,
            T payload,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            string json = JsonSerializer.Serialize(payload);
            using var request = CreateJsonPutRequest(endpoint, json, headers);
            LogRequest(request, json, "[HTTP-PUT]");
            using var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();
            LogResponse(response, endpoint, responseBody, "[HTTP-PUT]");

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiRequestException(
                    HttpMethod.Put,
                    endpoint,
                    response.StatusCode,
                    responseBody);
            }
        }

        public async Task DeleteJsonAsync<T>(
            string endpoint,
            T payload,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            string json = JsonSerializer.Serialize(payload);
            using var request = DeleteJsonRequest(endpoint, json, headers);
            LogRequest(request, json, "[HTTP-DELETE]");
            using var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();
            LogResponse(response, endpoint, responseBody, "[HTTP-DELETE]");

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiRequestException(
                    HttpMethod.Delete,
                    endpoint,
                    response.StatusCode,
                    responseBody);
            }
        }

        public async Task<TResponse?> DeleteJsonAsync<TRequest, TResponse>(
            string endpoint,
            TRequest payload,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            string json = JsonSerializer.Serialize(payload);
            using var request = DeleteJsonRequest(endpoint, json, headers);
            LogRequest(request, json, "[HTTP-DELETE]");
            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            LogResponse(response, endpoint, responseBody, "[HTTP-DELETE]");

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiRequestException(
                    HttpMethod.Delete,
                    endpoint,
                    response.StatusCode,
                    responseBody);
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

        public async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(
            string endpoint,
            TRequest payload,
            IReadOnlyDictionary<string, string>? headers)
        {
            string json = JsonSerializer.Serialize(payload);
            using var request = CreateJsonPostRequest(endpoint, json, headers);
            LogRequest(request, json, "[HTTP-POST]");
            using var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();
            LogResponse(response, endpoint, responseBody, "[HTTP-POST]");

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiRequestException(
                    HttpMethod.Post,
                    endpoint,
                    response.StatusCode,
                    responseBody);
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

            LogRequest(request, payloadJson: null, "[HTTP-GET]");

            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            LogResponse(response, endpoint, responseBody, "[HTTP-GET]");

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiRequestException(
                    HttpMethod.Get,
                    endpoint,
                    response.StatusCode,
                    responseBody);
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

        private static void LogRequest(HttpRequestMessage request, string? payloadJson, string prefix)
        {
            Debug.WriteLine($"{prefix} Request: {request.Method} {request.RequestUri}");
            Debug.WriteLine($"{prefix} Request headers: {FormatHeaders(request)}");

            if (!string.IsNullOrWhiteSpace(payloadJson))
            {
                Debug.WriteLine($"{prefix} Request payload: {payloadJson}");
            }
        }

        private static void LogResponse(HttpResponseMessage response, string endpoint, string? responseBody, string prefix)
        {
            Debug.WriteLine(
                $"{prefix} Response: {endpoint} Status={(int)response.StatusCode} ({response.StatusCode}) Headers: {FormatHeaders(response)}");

            if (responseBody != null)
            {
                Debug.WriteLine($"{prefix} Response body: {responseBody}");
            }
        }

        private static string FormatHeaders(HttpRequestMessage request)
        {
            IEnumerable<string> requestHeaders = request.Headers.Select(header =>
                $"{header.Key}={string.Join(",", header.Value)}");
            IEnumerable<string> contentHeaders = request.Content?.Headers.Select(header =>
                $"{header.Key}={string.Join(",", header.Value)}") ?? Enumerable.Empty<string>();

            return string.Join("; ", requestHeaders.Concat(contentHeaders));
        }

        private static string FormatHeaders(HttpResponseMessage response)
        {
            IEnumerable<string> responseHeaders = response.Headers.Select(header =>
                $"{header.Key}={string.Join(",", header.Value)}");
            IEnumerable<string> contentHeaders = response.Content.Headers.Select(header =>
                $"{header.Key}={string.Join(",", header.Value)}");

            return string.Join("; ", responseHeaders.Concat(contentHeaders));
        }
    }
}