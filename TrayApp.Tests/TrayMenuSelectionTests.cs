using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;
using TrayApp.TrayMenu.Controls;
using TrayApp.TrayMenu.Presenters;
using Xunit;

namespace TrayApp.Tests;

public sealed class TrayMenuSelectionTests
{
    [Theory]
    [InlineData("", "All groups")]
    [InlineData("work", "Work")]
    [InlineData("__ungrouped__", "Ungrouped")]
    public void RefreshSelections_InCredentials_KeepsGroupsAndSelectedEntry(string groupId, string expectedLabel)
    {
        RunOnStaThread(() =>
        {
            using var selectors = new SelectorFixture();
            selectors.Controller.SetActiveSection(TrayMenuSection.Credentials);
            selectors.Presenter.SelectedGroupId = groupId;
            selectors.Presenter.SelectedEntryId = "7";
            selectors.Service.Entries = new[]
            {
                new CredentialEntryListItem(7, "Example entry",
                    groupId == "__ungrouped__" ? "" : "work", "Work", "", "", "")
            };
            selectors.Presenter.LoadAsync(forceRefresh: true).GetAwaiter().GetResult();
            Invoke(selectors.Form, "PopulateCredentialSelectors");

            Invoke(selectors.Form, "RefreshQuickPanelSelections");
            Invoke(selectors.Form, "PopulateCredentialSelectors");

            Assert.Equal(new[] { "All groups", "Ungrouped", "Work" },
                selectors.Groups.Items.Cast<object>().Select(item => item.ToString()));
            Assert.Equal(expectedLabel, selectors.Groups.SelectedItem?.ToString());
            Assert.Equal("Example entry", selectors.Entries.SelectedItem?.ToString());
        });
    }

    [Fact]
    public void RefreshSelections_AfterLeavingCredentials_InvalidatesCacheBeforeReturning()
    {
        RunOnStaThread(() =>
        {
            using var selectors = new SelectorFixture();
            Invoke(selectors.Form, "PopulateCredentialSelectors");

            selectors.Controller.SetActiveSection(TrayMenuSection.Login);
            Invoke(selectors.Form, "RefreshQuickPanelSelections");
            Assert.Empty(selectors.Login.ProfileSelector.Items);
            Assert.Empty(selectors.Login.ServerSelector.Items);
            Assert.Equal(new[] { "All groups", "Ungrouped", "Work" },
                selectors.Groups.Items.Cast<object>().Select(item => item.ToString()));

            selectors.Controller.SetActiveSection(TrayMenuSection.Credentials);
            Invoke(selectors.Form, "RefreshQuickPanelSelections");

            Assert.Equal(new[] { "All groups", "Ungrouped", "Work" },
                selectors.Groups.Items.Cast<object>().Select(item => item.ToString()));
            Assert.Equal("Select entries", selectors.Entries.SelectedItem?.ToString());
        });
    }

    [Fact]
    public void WorkspaceSaveClick_InvokesPresenterCommand()
    {
        RunOnStaThread(() =>
        {
            using var selectors = new SelectorFixture();
            Invoke(selectors.Form, "InitializeCredentialWorkspacePanel");
            selectors.Presenter.WorkspaceMode = CredentialWorkspaceMode.CreateEntry;
            selectors.Workspace.LabelTextBox.Text = "Created through the view";

            typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(selectors.Workspace.SaveButton, new object[] { EventArgs.Empty });

            Assert.Equal(1, selectors.Service.SaveCount);
            Assert.Equal("9", selectors.Presenter.SelectedEntryId);
            Assert.Contains(selectors.Presenter.Entries, entry => entry.Label == "Created through the view");
        });
    }

    [Fact]
    public void CredentialGroupChange_UsesItsOwnSelectorAndResetsEntry()
    {
        RunOnStaThread(() =>
        {
            using var selectors = new SelectorFixture();
            Invoke(selectors.Form, "InitializeCredentialWorkspacePanel");
            Invoke(selectors.Form, "PopulateCredentialSelectors");
            selectors.Presenter.SelectedEntryId = "7";
            SetField(selectors.Form, "_isDeviceRegistered", false);

            selectors.Groups.SelectedIndex = 2;

            Assert.Equal("work", selectors.Presenter.SelectedGroupId);
            Assert.Empty(selectors.Presenter.SelectedEntryId);
            Assert.Empty(selectors.Login.ProfileSelector.Items);
            Assert.Empty(selectors.Login.ServerSelector.Items);
        });
    }

    private sealed class SelectorFixture : IDisposable
    {
        private readonly string _configPath = Path.GetTempFileName();
        public Form1 Form { get; }
        public TrayMenuController Controller { get; } = new((_, _) => { }, () => { });
        private CredentialSelectionControl Selection { get; } = new();
        public TrayDropdown Groups => Selection.GroupSelector;
        public TrayDropdown Entries => Selection.EntrySelector;
        public LoginControl Login { get; } = new();
        public CredentialWorkspaceControl Workspace { get; } = new();
        public FakeCredentialCollectionService Service { get; } = new()
        {
            Entries = Array.Empty<CredentialEntryListItem>()
        };
        public CredentialsPresenter Presenter { get; }

        public SelectorFixture()
        {
            // Isolate selector logic without starting Form1's registration, pipes or background listeners.
            Form = (Form1)RuntimeHelpers.GetUninitializedObject(typeof(Form1));
            File.WriteAllText(_configPath, "[]");
            var store = (RegistrationConfigStore)RuntimeHelpers.GetUninitializedObject(typeof(RegistrationConfigStore));
            SetField(store, "filePath", _configPath);
            var service = (RegisteredDeviceService)RuntimeHelpers.GetUninitializedObject(typeof(RegisteredDeviceService));
            SetField(service, "_configStore", store);
            SetField(Form, "_registeredDeviceService", service);
            SetField(Form, "_isDeviceRegistered", true);
            SetField(Form, "_trayMenuController", Controller);
            SetField(Form, "_loginView", Login);
            SetField(Form, "_credentialSelection", Selection);
            SetField(Form, "_credentialWorkspace", Workspace);
            Presenter = new CredentialsPresenter(Service, () => "profile|database");
            Presenter.LoadAsync().GetAwaiter().GetResult();
            SetField(Form, "_credentialsPresenter", Presenter);
        }

        public void Dispose()
        {
            Selection.Dispose();
            Workspace.Dispose();
            Login.Dispose();
            File.Delete(_configPath);
        }
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    private static void Invoke(Form1 form, string name)
    {
        MethodInfo method = typeof(Form1).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(Form1), name);
        method.Invoke(form, null);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
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
}
