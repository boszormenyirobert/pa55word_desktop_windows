using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using TrayApp.Models;
using TrayApp.Services;
using Xunit;

namespace TrayApp.Tests;

public sealed class ServiceRefactorTests
{
    [Theory]
    [InlineData("""{"accountId":"account","desktopDeviceId":"desktop","namespaces":[{"namespaceId":"server"}]}""")]
    [InlineData("""{"dek":{"accountId":"account","desktopDeviceId":"desktop"},"namespaces":[{"namespaceId":"server"}]}""")]
    [InlineData("""{"DEK":{"accountId":"account","desktopDeviceId":"desktop"},"NAMESPACES":[{"namespaceId":"server"}]}""")]
    public void RegistrationParser_PreservesFlatWrappedAndCaseInsensitiveResponses(string json)
    {
        DesktopRegistration? registration = RegistrationConfigStore.ParseRegistrationResponse(json);

        Assert.NotNull(registration);
        Assert.Equal("account", registration.AccountId);
        Assert.Equal("desktop", registration.DesktopDeviceId);
        Assert.Equal("server", Assert.Single(registration.Namespaces).NamespaceId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("null")]
    public void RegistrationParser_EmptyResponse_RemainsNull(string json)
    {
        Assert.Null(RegistrationConfigStore.ParseRegistrationResponse(json));
    }

    [Fact]
    public void RegistrationParser_MalformedJson_IsNotSilentlyAccepted()
    {
        Assert.Throws<JsonException>(() => RegistrationConfigStore.ParseRegistrationResponse("{broken"));
    }

    [Fact]
    public void RegistrationParser_WrappedResponseWithoutNamespaces_UsesEmptyCollection()
    {
        DesktopRegistration? registration = RegistrationConfigStore.ParseRegistrationResponse(
            """{"dek":{"accountId":"account"},"namespaces":null}""");

        Assert.NotNull(registration);
        Assert.Empty(registration.Namespaces);
    }

    [Fact]
    public void BootstrapConfiguration_MissingConfig_DoesNotCreateConfigAndReturnsFalse()
    {
        string path = Path.Combine(Path.GetTempPath(), $"trayapp-refactor-{Guid.NewGuid():N}.json");
        try
        {
            BootstrapRegistrationService service = CreateBootstrapService(path);

            Assert.False(service.LoadOrCreateConfig());
            Assert.False(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("[]", false)]
    [InlineData("""[{"userId":"user","namespaces":[{"namespaceId":"server","url":"https://example.test","accountId":"account","desktopDeviceId":"desktop","encDek":"encrypted"}]}]""", true)]
    public void BootstrapConfiguration_ExistingConfig_PreservesContents(string json, bool expected)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            BootstrapRegistrationService service = CreateBootstrapService(path);

            Assert.Equal(expected, service.LoadOrCreateConfig());
            Assert.Equal(json, File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RegistrationQr_IsOwnedByEachBootstrapService()
    {
        var first = new BootstrapRegistrationService();
        var second = new BootstrapRegistrationService();
        var qr = (QrService)typeof(BootstrapRegistrationService).GetField(
            "_qrService", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(first)!;
        try
        {
            qr.GenerateQrCode("""{"device":"first"}""");

            Assert.NotNull(first.QrCode);
            Assert.Null(second.QrCode);
        }
        finally
        {
            first.QrCode?.Dispose();
        }
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("GET")]
    public async Task HttpVerbs_InvalidJson_IsNotSilentlyAccepted(string verb)
    {
        using var httpClient = new HttpClient(new StubHttpHandler(HttpStatusCode.OK, "{broken"));

        await Assert.ThrowsAsync<JsonException>(() => SendAsync(CreateApi(httpClient), verb));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("GET")]
    public async Task HttpVerbs_PreserveCaseInsensitiveJsonAndRequestHeaders(string verb)
    {
        using var handler = new StubHttpHandler(HttpStatusCode.OK, """{"AUTHORIZED":false,"STATUS":"no_devices"}""");
        using var httpClient = new HttpClient(handler);
        HttpApiClient api = CreateApi(httpClient);

        AuthenticationResponseDto? response = await SendAsync(api, verb);

        Assert.NotNull(response);
        Assert.False(response.Authorized);
        Assert.Equal("no_devices", response.Status);
        Assert.Equal(verb, handler.Method);
        Assert.Equal("test-signature", handler.Signature);
        Assert.Equal(verb == "GET" ? null : """{"accountId":"account"}""", handler.Payload);
    }

    [Theory]
    [InlineData("POST", "")]
    [InlineData("PUT", "   ")]
    [InlineData("DELETE", "")]
    [InlineData("GET", " ")]
    public async Task HttpVerbs_EmptySuccessBody_PreservesNullResponse(string verb, string body)
    {
        using var httpClient = new HttpClient(new StubHttpHandler(HttpStatusCode.OK, body));

        Assert.Null(await SendAsync(CreateApi(httpClient), verb));
    }

    [Fact]
    public async Task AccountDeviceDeletion_NoContentResponse_IsSuccessful()
    {
        using var handler = new StubHttpHandler(HttpStatusCode.NoContent, string.Empty);
        using var httpClient = new HttpClient(handler);
        var service = new AccountDeviceService(CreateApi(httpClient), null!);
        string desktopDeviceId = $"test-device-{Guid.NewGuid():N}";
        var pki = new Pki();
        pki.CreatePrivateKey("KeyContainer_" + desktopDeviceId);

        try
        {
            await service.DeleteDeviceAsync(
                new AccountDeviceDto { MobileDeviceId = "mobile-device" },
                "https://example.test",
                "account",
                desktopDeviceId);

            Assert.Equal("DELETE", handler.Method);
        }
        finally
        {
            pki.DeletePrivateKey("KeyContainer_" + desktopDeviceId);
        }
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("GET")]
    public async Task HttpVerbs_BackendFailure_PreservesApiException(string verb)
    {
        using var httpClient = new HttpClient(new StubHttpHandler(HttpStatusCode.Unauthorized, "backend failure"));

        ApiRequestException error = await Assert.ThrowsAsync<ApiRequestException>(() =>
            SendAsync(CreateApi(httpClient), verb));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Equal("backend failure", error.ResponseBody);
    }

    [Fact]
    public void ExtensionSnapshots_RemainDetachedAndNotifyOnlyOnChanges()
    {
        var store = new ExtensionSessionStateStore();
        var events = new List<ExtensionSessionStateResponseDto>();
        store.StateChanged += events.Add;
        ExtensionSessionStateResponseDto initial = store.GetSnapshot();
        initial.Authorized = true;

        Assert.False(store.GetSnapshot().Authorized);
        store.MarkAuthorized();
        store.MarkAuthorized();
        Assert.Single(events);
        Assert.Equal(1, events[0].AuthVersion);
        store.MarkInvalid("expired");
        store.MarkInvalid("expired");

        Assert.Equal(2, events.Count);
        Assert.True(events[0].Authorized);
        Assert.False(events[1].Authorized);
        Assert.Equal(2, events[1].AuthVersion);
        Assert.Equal("expired", store.GetSnapshot().Reason);
    }

    private static BootstrapRegistrationService CreateBootstrapService(string path)
    {
        var config = new RegistrationConfigStore();
        typeof(RegistrationConfigStore).GetField("filePath", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(config, path);
        var service = new BootstrapRegistrationService();
        typeof(BootstrapRegistrationService).GetField("_configStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, config);
        return service;
    }

    private static HttpApiClient CreateApi(HttpClient httpClient)
    {
        var api = (HttpApiClient)RuntimeHelpers.GetUninitializedObject(typeof(HttpApiClient));
        typeof(HttpApiClient).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(api, httpClient);
        return api;
    }

    private static Task<AuthenticationResponseDto?> SendAsync(HttpApiClient api, string verb)
    {
        const string endpoint = "https://example.test/api/v1/authentication";
        var payload = new { accountId = "account" };
        var headers = new Dictionary<string, string> { ["x-signature"] = "test-signature" };
        return verb switch
        {
            "POST" => api.PostJsonAsync<object, AuthenticationResponseDto>(endpoint, payload, headers),
            "PUT" => api.PutJsonAsync<object, AuthenticationResponseDto>(endpoint, payload, headers),
            "DELETE" => api.DeleteJsonAsync<object, AuthenticationResponseDto>(endpoint, payload, headers),
            "GET" => api.GetJsonAsync<AuthenticationResponseDto>(endpoint, headers),
            _ => throw new ArgumentOutOfRangeException(nameof(verb))
        };
    }

    private sealed class StubHttpHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public string? Method { get; private set; }
        public string? Signature { get; private set; }
        public string? Payload { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method.Method;
            Signature = request.Headers.GetValues("x-signature").Single();
            Payload = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
