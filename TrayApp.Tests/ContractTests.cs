using System.Text.Json;
using TrayApp.Services;
using Xunit;

namespace TrayApp.Tests;

public sealed class ContractTests
{
    [Theory]
    [InlineData("https://api.example.test", "credentials", "https://api.example.test/api/v1/credentials")]
    [InlineData("https://api.example.test/", "/credentials", "https://api.example.test/api/v1/credentials")]
    public void BuildApiUrl_NormalizesUrlAndRoute(string baseUrl, string route, string expectedUrl)
    {
        Assert.Equal(expectedUrl, AppEnvironment.BuildApiUrl(baseUrl, route));
    }

    [Fact]
    public void CreateErrorResponseJson_PreservesExtensionErrorContract()
    {
        string responseJson = BrowserCommandHandler.CreateErrorResponseJson("processing_error", "Request failed.");

        using JsonDocument response = JsonDocument.Parse(responseJson);

        Assert.False(response.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("processing_error", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Request failed.", response.RootElement.GetProperty("error").GetProperty("message").GetString());
    }
}