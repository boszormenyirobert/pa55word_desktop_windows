using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;
using TrayApp.TrayMenu.Controls;
using Xunit;

namespace TrayApp.Tests;

[CollectionDefinition("Account rename", DisableParallelization = true)]
public sealed class AccountRenameCollection;

[Collection("Account rename")]
public sealed class AccountRenameTests
{
    [Fact]
    public void ProfileView_OffersRenameFormAndRequiresSignedInProfile()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var fixture = new RenameFixture();
                var form = (Form1)RuntimeHelpers.GetUninitializedObject(typeof(Form1));
                using var detail = new Panel();
                using var actions = new TrayDropdown();
                using var renamePanel = new Panel();
                using var name = new TextBox();
                using var submit = new Button();
                using var accounts = new FlowLayoutPanel();
                using var guidance = new Panel();
                using var description = new Label();
                using var selection = new Panel();
                using var metrics = new Panel();
                using var insight = new Panel();
                var controller = new TrayMenuController((_, _) => { }, () => { });
                controller.SetActiveSection(TrayMenuSection.SettingsUsers);
                void Set(string field, object value) => typeof(Form1)
                    .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, value);
                void Invoke(string method) => typeof(Form1)
                    .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
                Set("_quickPanelDetailPanel", detail);
                Set("_quickPanelSettingsUserActionComboBox", actions);
                Set("_quickPanelAccountRenamePanel", renamePanel);
                Set("_quickPanelAccountNameTextBox", name);
                Set("_quickPanelAccountRenameSubmitButton", submit);
                Set("_quickPanelAccountsPanel", accounts);
                Set("_quickPanelAccountsGuidancePanel", guidance);
                Set("_quickPanelDetailDescriptionLabel", description);
                Set("_quickPanelSelectionCard", selection);
                Set("_quickPanelMetricsCard", metrics);
                Set("_quickPanelInsightCard", insight);
                Set("_authenticationService", fixture.Authentication);
                Set("_trayMenuController", controller);
                Set("_showInlineAccountRename", true);
                Set("_accountRenameUserId", "owner");

                Invoke("InitializeAccountRenameControls");
                Invoke("UpdateAccountRenamePresentation");
                Assert.Contains(name, renamePanel.Controls.Cast<Control>());
                Assert.Contains(submit, renamePanel.Controls.Cast<Control>());
                Assert.False(submit.Enabled);

                fixture.SignIn("owner");
                Invoke("PopulateSettingsUserActions");
                Invoke("UpdateAccountRenamePresentation");
                Assert.Contains("Rename profile", actions.Items.Cast<object>().Select(item => item.ToString()));
                Assert.True(submit.Enabled);
                Assert.True(renamePanel.Visible);
                controller.SetActiveSection(TrayMenuSection.Accounts);
                Invoke("UpdateAccountRenamePresentation");
                Assert.False(renamePanel.Visible);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [Fact]
    public void Rename_OneSignedInServer_ChangesOnlyOwnName()
    {
        using var fixture = new RenameFixture();
        fixture.SignIn("owner");
        JsonNode before = JsonNode.Parse(File.ReadAllText(fixture.ConfigPath))!;

        fixture.Authentication.RenameProfile("owner", "  New account name  ");

        JsonNode after = JsonNode.Parse(File.ReadAllText(fixture.ConfigPath))!;
        before[0]!["userName"] = "New account name";
        Assert.True(JsonNode.DeepEquals(before, after));
        Assert.Single(fixture.Sessions.GetStates("owner"));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("other-profile")]
    [InlineData("expired")]
    [InlineData("wrong-device")]
    public void Rename_WithoutOwnValidSession_LeavesConfigUnchanged(string session)
    {
        using var fixture = new RenameFixture();
        if (session != "none")
        {
            fixture.SignIn(session == "other-profile" ? "other" : "owner",
                expired: session == "expired", wrongDevice: session == "wrong-device");
        }
        string before = File.ReadAllText(fixture.ConfigPath);

        Assert.Throws<InvalidOperationException>(() =>
            fixture.Authentication.RenameProfile("owner", "Changed"));

        Assert.Equal(before, File.ReadAllText(fixture.ConfigPath));
    }

    [Fact]
    public void Rename_AnotherProfile_LeavesConfigUnchanged()
    {
        using var fixture = new RenameFixture();
        fixture.SignIn("owner");
        fixture.SignIn("other");
        string before = File.ReadAllText(fixture.ConfigPath);

        Assert.Throws<InvalidOperationException>(() =>
            fixture.Authentication.RenameProfile("other", "Changed"));

        Assert.Equal(before, File.ReadAllText(fixture.ConfigPath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_EmptyName_LeavesConfigUnchanged(string name)
    {
        using var fixture = new RenameFixture();
        fixture.SignIn("owner");
        string before = File.ReadAllText(fixture.ConfigPath);

        Assert.Throws<ArgumentException>(() => fixture.Authentication.RenameProfile("owner", name));

        Assert.Equal(before, File.ReadAllText(fixture.ConfigPath));
    }

    [Fact]
    public void Rename_UnknownProfile_DoesNotRenameFallbackProfile()
    {
        using var fixture = new RenameFixture();
        string before = File.ReadAllText(fixture.ConfigPath);

        Assert.Throws<InvalidOperationException>(() =>
            fixture.Config.RenameRegistration("missing", "Changed"));

        Assert.Equal(before, File.ReadAllText(fixture.ConfigPath));
    }

    private sealed class RenameFixture : IDisposable
    {
        private readonly string _sessionsPath = Path.GetTempFileName();
        private readonly FieldInfo _selection = GetField(typeof(AppEnvironment), "_activeSelection", isStatic: true);
        private readonly object? _previousSelection;
        public string ConfigPath { get; } = Path.GetTempFileName();
        public RegistrationConfigStore Config { get; }
        public AuthorizationSessionStore Sessions { get; }
        public AuthenticationService Authentication { get; }

        public RenameFixture()
        {
            Config = (RegistrationConfigStore)RuntimeHelpers.GetUninitializedObject(typeof(RegistrationConfigStore));
            GetField(typeof(RegistrationConfigStore), "filePath").SetValue(Config, ConfigPath);
            File.WriteAllText(ConfigPath, "[]");
            foreach (string user in new[] { "owner", "other" })
            {
                Config.SaveRegistration(new DesktopRegistration
                {
                    UserId = user,
                    UserName = user,
                    Namespaces = Enumerable.Range(1, 2).Select(index => new NamespaceItemDto
                    {
                        NamespaceId = $"{user}-{index}",
                        Url = $"https://server-{index}.example.test",
                        AccountId = $"{user}-account-{index}",
                        DesktopDeviceId = $"{user}-desktop-{index}",
                        EncDek = "test-dek"
                    }).ToList()
                });
            }

            Sessions = (AuthorizationSessionStore)RuntimeHelpers.GetUninitializedObject(typeof(AuthorizationSessionStore));
            GetField(typeof(AuthorizationSessionStore), "_syncRoot").SetValue(Sessions, new object());
            GetField(typeof(AuthorizationSessionStore), "_filePath").SetValue(Sessions, _sessionsPath);
            FieldInfo snapshots = GetField(typeof(AuthorizationSessionStore), "_snapshots");
            snapshots.SetValue(Sessions, Activator.CreateInstance(snapshots.FieldType));
            var devices = (RegisteredDeviceService)RuntimeHelpers.GetUninitializedObject(typeof(RegisteredDeviceService));
            GetField(typeof(RegisteredDeviceService), "_configStore").SetValue(devices, Config);
            Authentication = new AuthenticationService(devices, new HttpApiClient(), Sessions);

            _previousSelection = _selection.GetValue(null);
            // Keep the real config and authorization files untouched.
            _selection.SetValue(null, Activator.CreateInstance(_selection.FieldType, "owner", "owner-2"));
        }

        public void SignIn(string user, bool expired = false, bool wrongDevice = false)
        {
            Sessions.SaveActive(user, $"{user}-1", "https://server-1.example.test", $"{user}-account-1",
                wrongDevice ? "wrong-device" : $"{user}-desktop-1",
                DateTimeOffset.UtcNow.AddHours(expired ? -1 : 1), null);
        }

        public void Dispose()
        {
            _selection.SetValue(null, _previousSelection);
            File.Delete(ConfigPath);
            File.Delete(_sessionsPath);
        }

        private static FieldInfo GetField(Type type, string name, bool isStatic = false)
        {
            return type.GetField(name, BindingFlags.NonPublic |
                (isStatic ? BindingFlags.Static : BindingFlags.Instance))
                ?? throw new MissingFieldException(type.Name, name);
        }
    }
}
