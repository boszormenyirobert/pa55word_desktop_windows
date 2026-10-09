using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using TrayApp.Models;
using TrayApp.Services;
using Xunit;

namespace TrayApp.Tests;

public sealed class ProfileDeletionTests
{
    [Fact]
    public async Task Delete_RemovesEveryServerUsingItsOwnDeviceAndPreservesOtherProfiles()
    {
        using var fixture = new DeletionFixture();
        NamespaceItemDto[] servers = fixture.Profile.Namespaces.ToArray();
        var deleted = new List<(string Url, string AccountId, string DeviceId)>();
        fixture.SignInToAllServers();
        fixture.SignIn(fixture.OtherProfile.UserKey, fixture.OtherProfile.Namespaces[0]);
        var deleteProfile = fixture.CreateDeletion(server =>
        {
            Assert.True(fixture.Config.TryReadRegistration(fixture.Profile.UserKey, out _));
            deleted.Add((server.Url, server.AccountId, server.DesktopDeviceId));
            return Task.CompletedTask;
        });

        await deleteProfile(fixture.Profile.UserKey);

        Assert.Equal(servers.Select(server => (server.Url, server.AccountId, server.DesktopDeviceId)), deleted);
        Assert.Equal(fixture.OtherProfile.UserKey, Assert.Single(fixture.Config.ReadRegistrations()).UserKey);
        Assert.Empty(fixture.Sessions.GetStates(fixture.Profile.UserKey));
        Assert.Single(fixture.Sessions.GetStates(fixture.OtherProfile.UserKey));
    }

    [Fact]
    public async Task Delete_FinalServer_RemovesLastProfileAndItsSessions()
    {
        using var fixture = new DeletionFixture();
        fixture.Config.RemoveRegistration(fixture.OtherProfile.UserKey);
        fixture.SignInToAllServers();
        int requests = 0;
        var deleteProfile = fixture.CreateDeletion(_ =>
        {
            requests++;
            return Task.CompletedTask;
        });

        await deleteProfile(fixture.Profile.UserKey);

        Assert.Equal(2, requests);
        Assert.False(fixture.Config.TryReadRegistrations(out _));
        Assert.Empty(fixture.Sessions.GetStates(fixture.Profile.UserKey));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeviceNotFound_LocalCleanupCanBeDisabledForProfileDeletion(bool removeLocalRegistration)
    {
        using var fixture = new DeletionFixture();
        fixture.SignInToAllServers();
        NamespaceItemDto server = fixture.Profile.Namespaces[0];
        var registeredDeviceService = (RegisteredDeviceService)RuntimeHelpers.GetUninitializedObject(
            typeof(RegisteredDeviceService));
        typeof(RegisteredDeviceService).GetField("_configStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(registeredDeviceService, fixture.Config);
        var httpClient = new HttpApiClient();
        var authenticationService = new AuthenticationService(registeredDeviceService, httpClient, fixture.Sessions);
        var accountDeviceService = new AccountDeviceService(httpClient, authenticationService);
        var remoteError = new ApiRequestException(HttpMethod.Delete,
            AppEnvironment.BuildApiUrl(server.Url, ApiRoutes.AccountDevices),
            HttpStatusCode.Unauthorized, "e_40101_2: Device not found for signature verification");
        Func<Task<AccountDeviceDeleteResponseDto>> send = () => Task.FromException<AccountDeviceDeleteResponseDto>(remoteError);
        MethodInfo method = typeof(AccountDeviceService)
            .GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(AccountDeviceDeleteResponseDto));
        var request = (Task<AccountDeviceDeleteResponseDto>)method.Invoke(accountDeviceService,
            new object[] { server.DesktopDeviceId, send, removeLocalRegistration })!;

        if (removeLocalRegistration)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => request);
            Assert.Single(fixture.Config.ReadRegistration(fixture.Profile.UserKey).Namespaces);
            Assert.Single(fixture.Sessions.GetStates(fixture.Profile.UserKey));
        }
        else
        {
            ApiRequestException error = await Assert.ThrowsAsync<ApiRequestException>(() => request);
            Assert.Same(remoteError, error);
            Assert.Equal(2, fixture.Config.ReadRegistration(fixture.Profile.UserKey).Namespaces.Count);
            Assert.Equal(2, fixture.Sessions.GetStates(fixture.Profile.UserKey).Count);
        }
    }

    [Fact]
    public async Task Delete_MissingSessionOnOneServer_DoesNotDeleteAnything()
    {
        using var fixture = new DeletionFixture();
        fixture.SignIn(fixture.Profile.UserKey, fixture.Profile.Namespaces[0]);
        var deleted = new List<string>();
        var deleteProfile = fixture.CreateDeletion(server =>
        {
            deleted.Add(server.Url);
            return Task.CompletedTask;
        });

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => deleteProfile(fixture.Profile.UserKey));

        Assert.Contains(fixture.Profile.Namespaces[1].Url, error.Message);
        Assert.Empty(deleted);
        Assert.Equal(2, fixture.Config.ReadRegistration(fixture.Profile.UserKey).Namespaces.Count);
        Assert.Single(fixture.Sessions.GetStates(fixture.Profile.UserKey));
    }

    [Fact]
    public async Task Delete_AnotherProfilesSessions_DoNotAuthorizeDeletion()
    {
        using var fixture = new DeletionFixture();
        foreach (NamespaceItemDto server in fixture.Profile.Namespaces)
        {
            fixture.SignIn(fixture.OtherProfile.UserKey, server);
        }
        int requests = 0;
        var deleteProfile = fixture.CreateDeletion(_ =>
        {
            requests++;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => deleteProfile(fixture.Profile.UserKey));

        Assert.Equal(0, requests);
        Assert.Equal(2, fixture.Config.ReadRegistration(fixture.Profile.UserKey).Namespaces.Count);
    }

    [Theory]
    [InlineData("namespace")]
    [InlineData("url")]
    [InlineData("account")]
    [InlineData("device")]
    [InlineData("expired")]
    public async Task Delete_MismatchedOrExpiredSession_DoesNotAuthorizeDeletion(string mismatch)
    {
        using var fixture = new DeletionFixture();
        NamespaceItemDto server = fixture.Profile.Namespaces[0];
        fixture.Sessions.SaveActive(
            fixture.Profile.UserKey,
            mismatch == "namespace" ? "different-namespace" : server.NamespaceId,
            mismatch == "url" ? "https://different.example.test" : server.Url,
            mismatch == "account" ? "different-account" : server.AccountId,
            mismatch == "device" ? "different-device" : server.DesktopDeviceId,
            mismatch == "expired" ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddHours(1),
            null);
        fixture.SignIn(fixture.Profile.UserKey, fixture.Profile.Namespaces[1]);
        int requests = 0;
        var deleteProfile = fixture.CreateDeletion(_ =>
        {
            requests++;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => deleteProfile(fixture.Profile.UserKey));

        Assert.Equal(0, requests);
        Assert.Equal(2, fixture.Config.ReadRegistration(fixture.Profile.UserKey).Namespaces.Count);
    }

    [Fact]
    public async Task Delete_PartialFailure_PersistsProgressAndRetriesOnlyRemainingServer()
    {
        using var fixture = new DeletionFixture();
        fixture.SignInToAllServers();
        NamespaceItemDto first = fixture.Profile.Namespaces[0];
        NamespaceItemDto second = fixture.Profile.Namespaces[1];
        var attempted = new List<string>();
        bool fail = true;
        var deleteProfile = fixture.CreateDeletion(server =>
        {
            attempted.Add(server.Url);
            return fail && server.NamespaceId == second.NamespaceId
                ? Task.FromException(new HttpRequestException("Server unavailable."))
                : Task.CompletedTask;
        });

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => deleteProfile(fixture.Profile.UserKey));

        Assert.Contains(second.Url, error.Message);
        Assert.IsType<HttpRequestException>(error.InnerException);
        Assert.Equal(second.NamespaceId,
            Assert.Single(fixture.Config.ReadRegistration(fixture.Profile.UserKey).Namespaces).NamespaceId);
        Assert.Equal(second.NamespaceId, Assert.Single(fixture.Sessions.GetStates(fixture.Profile.UserKey)).NamespaceId);

        fail = false;
        await deleteProfile(fixture.Profile.UserKey);

        Assert.Equal(new[] { first.Url, second.Url, second.Url }, attempted);
        Assert.DoesNotContain(fixture.Config.ReadRegistrations(), registration => registration.UserKey == fixture.Profile.UserKey);
        Assert.Empty(fixture.Sessions.GetStates(fixture.Profile.UserKey));
    }

    [Fact]
    public async Task Delete_FirstServerFails_PreservesAllRegistrationsAndSessions()
    {
        using var fixture = new DeletionFixture();
        fixture.SignInToAllServers();
        int requests = 0;
        var deleteProfile = fixture.CreateDeletion(_ =>
        {
            requests++;
            return Task.FromException(new HttpRequestException("Server unavailable."));
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => deleteProfile(fixture.Profile.UserKey));

        Assert.Equal(1, requests);
        Assert.Equal(2, fixture.Config.ReadRegistration(fixture.Profile.UserKey).Namespaces.Count);
        Assert.Equal(2, fixture.Sessions.GetStates(fixture.Profile.UserKey).Count);
    }

    [Fact]
    public async Task Delete_SessionRevokedDuringDeletion_StopsBeforeUnauthorizedRequest()
    {
        using var fixture = new DeletionFixture();
        fixture.SignInToAllServers();
        NamespaceItemDto first = fixture.Profile.Namespaces[0];
        NamespaceItemDto second = fixture.Profile.Namespaces[1];
        var attempted = new List<string>();
        var deleteProfile = fixture.CreateDeletion(server =>
        {
            attempted.Add(server.Url);
            fixture.Sessions.Revoke(fixture.Profile.UserKey, second.NamespaceId);
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => deleteProfile(fixture.Profile.UserKey));

        Assert.Equal(new[] { first.Url }, attempted);
        Assert.Equal(second.NamespaceId,
            Assert.Single(fixture.Config.ReadRegistration(fixture.Profile.UserKey).Namespaces).NamespaceId);
    }

    [Fact]
    public async Task Delete_SharedDeviceOnSameServer_IsDeletedOnceForAllNamespaces()
    {
        using var fixture = new DeletionFixture();
        NamespaceItemDto first = fixture.Profile.Namespaces[0];
        NamespaceItemDto second = fixture.Profile.Namespaces[1];
        second.Url = first.Url.ToUpperInvariant() + "/";
        second.AccountId = first.AccountId;
        second.DesktopDeviceId = first.DesktopDeviceId;
        fixture.Config.SaveRegistration(fixture.Profile);
        fixture.SignInToAllServers();
        int requests = 0;
        var deleteProfile = fixture.CreateDeletion(_ =>
        {
            requests++;
            return Task.CompletedTask;
        });

        await deleteProfile(fixture.Profile.UserKey);

        Assert.Equal(1, requests);
        Assert.Equal(fixture.OtherProfile.UserKey, Assert.Single(fixture.Config.ReadRegistrations()).UserKey);
        Assert.Empty(fixture.Sessions.GetStates(fixture.Profile.UserKey));
    }

    [Fact]
    public async Task Delete_UnknownProfile_DoesNotFallBackToAnotherProfile()
    {
        using var fixture = new DeletionFixture();
        fixture.SignInToAllServers();
        int requests = 0;
        var deleteProfile = fixture.CreateDeletion(_ =>
        {
            requests++;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => deleteProfile("unknown-profile"));

        Assert.Equal(0, requests);
        Assert.Equal(2, fixture.Config.ReadRegistrations().Count);
    }

    private sealed class DeletionFixture : IDisposable
    {
        private readonly string _configPath = Path.GetTempFileName();
        private readonly string _sessionsPath = Path.GetTempFileName();
        public RegistrationConfigStore Config { get; }
        public AuthorizationSessionStore Sessions { get; }
        public DesktopRegistration Profile { get; } = CreateProfile("profile", 2);
        public DesktopRegistration OtherProfile { get; } = CreateProfile("other-profile", 1);

        public DeletionFixture()
        {
            Config = (RegistrationConfigStore)RuntimeHelpers.GetUninitializedObject(typeof(RegistrationConfigStore));
            SetField(Config, "filePath", _configPath);
            File.WriteAllText(_configPath, "[]");
            Config.SaveRegistration(Profile);
            Config.SaveRegistration(OtherProfile);

            // Use isolated stores without reading or writing the user's live authorization state.
            Sessions = (AuthorizationSessionStore)RuntimeHelpers.GetUninitializedObject(typeof(AuthorizationSessionStore));
            SetField(Sessions, "_syncRoot", new object());
            SetField(Sessions, "_filePath", _sessionsPath);
            FieldInfo snapshots = GetField(typeof(AuthorizationSessionStore), "_snapshots");
            snapshots.SetValue(Sessions, Activator.CreateInstance(snapshots.FieldType));
        }

        public Func<string, Task> CreateDeletion(Func<NamespaceItemDto, Task> deleteDevice)
        {
            return userId => Form1.DeleteProfileRegistrationsAsync(userId, Config, Sessions, deleteDevice);
        }

        public void SignInToAllServers()
        {
            foreach (NamespaceItemDto server in Profile.Namespaces)
            {
                SignIn(Profile.UserKey, server);
            }
        }

        public void SignIn(string userId, NamespaceItemDto server)
        {
            Sessions.SaveActive(userId, server.NamespaceId, server.Url, server.AccountId,
                server.DesktopDeviceId, DateTimeOffset.UtcNow.AddHours(1), null);
        }

        public void Dispose()
        {
            File.Delete(_configPath);
            File.Delete(_sessionsPath);
        }

        private static DesktopRegistration CreateProfile(string userId, int servers)
        {
            return new DesktopRegistration
            {
                UserId = userId,
                UserName = userId,
                Namespaces = Enumerable.Range(1, servers).Select(index => new NamespaceItemDto
                {
                    NamespaceId = $"{userId}-namespace-{index}",
                    Namespace = $"Server {index}",
                    Url = $"https://{userId}-server-{index}.example.test",
                    AccountId = $"{userId}-account-{index}",
                    DesktopDeviceId = $"{userId}-device-{index}",
                    EncDek = "test-encrypted-dek"
                }).ToList()
            };
        }

        private static void SetField(object target, string name, object value)
        {
            GetField(target.GetType(), name).SetValue(target, value);
        }

        private static FieldInfo GetField(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(type.Name, name);
        }
    }
}
