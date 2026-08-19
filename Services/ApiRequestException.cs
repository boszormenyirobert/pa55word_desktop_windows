using System.Net;

namespace TrayApp.Services
{
    internal sealed class ApiRequestException : HttpRequestException
    {
        public ApiRequestException(
            HttpMethod method,
            string endpoint,
            HttpStatusCode statusCode,
            string responseBody)
            : base($"{method} {endpoint} failed with {(int)statusCode} ({statusCode}). Body: {responseBody}", inner: null, statusCode)
        {
            Method = method;
            Endpoint = endpoint;
            ResponseBody = responseBody;
        }

        public HttpMethod Method { get; }

        public string Endpoint { get; }

        public string ResponseBody { get; }
    }
}