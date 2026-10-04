using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;
using TrayApp.TrayMenu.Controls;
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
            SetField(selectors.Form, "_selectedCredentialGroupId", groupId);
            SetField(selectors.Form, "_selectedCredentialEntryId", "7");
            SetField(selectors.Form, "_credentialEntries", new[]
            {
                new CredentialEntryListItem(7, "Example entry",
                    groupId == "__ungrouped__" ? "" : "work", "Work", "", "", "")
            });
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
            Assert.Empty(selectors.Groups.Items);

            selectors.Controller.SetActiveSection(TrayMenuSection.Credentials);
            Invoke(selectors.Form, "RefreshQuickPanelSelections");

            Assert.Equal(new[] { "All groups", "Ungrouped", "Work" },
                selectors.Groups.Items.Cast<object>().Select(item => item.ToString()));
            Assert.Equal("Select entries", selectors.Entries.SelectedItem?.ToString());
        });
    }

    private sealed class SelectorFixture : IDisposable
    {
        private readonly string _configPath = Path.GetTempFileName();
        public Form1 Form { get; }
        public TrayMenuController Controller { get; } = new((_, _) => { }, () => { });
        public TrayDropdown Groups { get; } = new();
        public TrayDropdown Entries { get; } = new();
        private TrayDropdown AssignedGroup { get; } = new();

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
            SetField(Form, "_quickPanelUserComboBox", Groups);
            SetField(Form, "_quickPanelNamespaceComboBox", Entries);
            SetField(Form, "_quickPanelCredentialAssignedGroupComboBox", AssignedGroup);
            SetField(Form, "_credentialGroups", new[]
            {
                new CredentialGroupDto { GroupId = "work", GroupName = "Work" }
            });
            SetField(Form, "_credentialEntries", Array.Empty<CredentialEntryListItem>());
        }

        public void Dispose()
        {
            Groups.Dispose();
            Entries.Dispose();
            AssignedGroup.Dispose();
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
