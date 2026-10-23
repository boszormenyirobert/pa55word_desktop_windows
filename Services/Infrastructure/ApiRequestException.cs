using System.Net;

namespace TrayApp.Services
{
    internal sealed class ApiRequestException : HttpRequestException
    {
        public ApiRequestException(
            HttpMethod method,
            string endpoint,
            HttpStatusCode statusCode,
            string responseBody,
            TimeSpan? retryAfter = null)
            : base($"{method} {endpoint} failed with {(int)statusCode} ({statusCode}). Body: {responseBody}", inner: null, statusCode)
        {
            Method = method;
            Endpoint = endpoint;
            ResponseBody = responseBody;
            RetryAfter = retryAfter;
        }

        public HttpMethod Method { get; }

        public string Endpoint { get; }

        public string ResponseBody { get; }
        public TimeSpan? RetryAfter { get; }
    }
}