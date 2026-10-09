using System.Reflection;
using System.Text.Json;
using TrayApp.Models;
using TrayApp.Services;
using Xunit;

namespace TrayApp.Tests;

public sealed class AuthenticationFailureTests
{
    [Theory]
    [InlineData("no_devices")]
    [InlineData("NO_DEVICES")]
    public async Task NoDevicesResponse_PreservesExplanationThroughFinalFailureNotification(string status)
    {
        var notifications = new List<string?>();
        var service = new AuthenticationService(
            null!, null!, null!, (message, _) => notifications.Add(message));
        AuthenticationResponseDto response = JsonSerializer.Deserialize<AuthenticationResponseDto>(
            $$"""{"authorized":false,"status":"{{status}}","notificationId":null,"timeoutSeconds":90}""")!;
        MethodInfo resolve = typeof(AuthenticationService).GetMethod(
            "ResolveAuthorizationResponseAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = (Task<bool>)resolve.Invoke(service, new object?[] { response, null })!;

        Assert.False(await result);
        typeof(AuthenticationService).GetMethod(
            "NotifyAuthorizationFailure", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, null);
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, message => Assert.Equal(
            "There is no mobile device registered to this account that can authorize the request.", message));
    }
}
