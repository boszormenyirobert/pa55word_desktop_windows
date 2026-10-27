using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using TrayApp.TrayMenu;
using TrayApp.TrayMenu.Controls;
using TrayApp.Services;
using Xunit;

namespace TrayApp.Tests;

public sealed class MainWindowPresentationTests
{
    [Fact]
    public void CredentialSelection_OwnsSelectorsAndPreservesEntryAndGroupActions()
    {
        RunOnStaThread(() =>
        {
            using var host = new Form { ClientSize = new Size(700, 800) };
            using var selection = new CredentialSelectionControl();
            using var groupEditor = new Panel { Bounds = new Rectangle(32, 300, 578, 200) };
            host.Controls.Add(selection);
            host.Controls.Add(groupEditor);
            host.Show();
            int creates = 0, groups = 0, changes = 0;
            selection.CreateRequested += (_, _) => creates++;
            selection.ManageGroupsRequested += (_, _) => groups++;
            selection.GroupChanged += (_, _) => changes++;
            selection.ApplyMode(false, false, false, false, groupEditor);
            Assert.Same(selection, selection.GroupSelector.Parent);
            Assert.Same(selection, selection.EntrySelector.Parent);
            Assert.Equal(new Rectangle(32, 142, 578, 74), selection.Bounds);
            Assert.Equal(new Point(16, 36), selection.GroupSelector.Location);
            Assert.Equal(new Point(308, 36), selection.EntrySelector.Location);
            Assert.Equal(244, selection.GroupSelector.Width);
            Assert.Equal(216, selection.EntrySelector.Width);
            Assert.True(selection.ClientRectangle.Contains(selection.GroupSelector.Bounds));
            Assert.True(selection.ClientRectangle.Contains(selection.EntrySelector.Bounds));
            selection.GroupSelector.Items.Add("All groups");
            selection.GroupSelector.SetSelectedIndexSilently(0);
            Assert.Equal(0, changes);
            selection.GroupSelector.Items.Add("Work");
            selection.GroupSelector.SelectedIndex = 1;
            selection.CreateButton.PerformClick();
            selection.ManageGroupsButton.PerformClick();
            Assert.Equal(new[] { 1, 1, 1 }, new[] { creates, groups, changes });

            selection.ApplyMode(true, false, false, false, groupEditor);
            Assert.False(selection.Visible);
            Assert.Same(groupEditor, selection.CreateButton.Parent);
            Assert.Equal("+ Create new group", selection.CreateButton.Text);
            Assert.Equal(new Point(372, 0), selection.CreateButton.Location);
            selection.CreateButton.PerformClick();
            Assert.Equal(2, creates);
            selection.ApplyMode(true, false, false, true, groupEditor);
            Assert.False(selection.CreateButton.Enabled);
            selection.ApplyMode(true, true, false, false, groupEditor);
            Assert.False(selection.CreateButton.Visible);
            selection.ApplyMode(false, false, true, false, groupEditor);
            Assert.False(selection.Visible);
            selection.ApplyMode(false, false, false, false, groupEditor);
            Assert.True(selection.Visible);
            Assert.Same(selection, selection.CreateButton.Parent);
            Assert.Equal("+ Create new entry", selection.CreateButton.Text);
        });
    }

    [Fact]
    public void AccountsView_OwnsRowsAndForwardsRemovalWithoutCallingServices()
    {
        RunOnStaThread(() =>
        {
            using var host = new Form { ClientSize = new Size(700, 800) };
            using var accounts = new AccountsControl { Visible = true };
            host.Controls.Add(accounts);
            host.Show();
            var server = new TrayApp.Models.NamespaceItemDto
            {
                NamespaceId = "server-one", Url = "https://server.example", AccountId = "account-one"
            };
            int removals = 0;
            accounts.RemoveRequested += (_, request) =>
            {
                Assert.Equal("profile-one", request.UserId);
                Assert.Same(server, request.NamespaceItem);
                removals++;
            };
            accounts.Render("profile-one", new[] { server });
            Control row = Assert.Single(accounts.ListPanel.Controls.Cast<Control>());
            Assert.Single(row.Controls.OfType<Button>()).PerformClick();
            Assert.Equal(1, removals);
            foreach (int height in new[] { 660, 900, 660 })
            {
                accounts.LayoutContent(height);
                Assert.Equal(height - 15, accounts.Top + accounts.ListPanel.Bottom);
                Assert.Equal(384, accounts.Top + accounts.ListPanel.Top);
            }
            accounts.Render(string.Empty, Array.Empty<TrayApp.Models.NamespaceItemDto>());
            Assert.True(row.IsDisposed);
            Assert.Empty(accounts.ListPanel.Controls);
        });
    }

    [Fact]
    public void SettingsView_OwnsNavigationAvailabilityAndForwardsRequestsOnce()
    {
        RunOnStaThread(() =>
        {
            using var host = new Form { ClientSize = new Size(700, 800) };
            using var settings = new SettingsControl();
            host.Controls.Add(settings);
            host.Show();
            int requests = 0, profileChanges = 0, opens = 0;
            settings.NavigationRequested += (_, request) => { Assert.Equal(TrayMenuSection.SettingsUsers, request.Section); requests++; };
            settings.RecoveryProfileChanged += (_, _) => profileChanges++;
            settings.RecoveryOpenRequested += (_, _) => opens++;
            settings.ApplyState(new SettingsViewState(true, true, false, "Settings description", "Login required"));
            settings.LayoutContent(800, true);
            Assert.Contains("Login required", settings.DescriptionBox.Text);
            Assert.Contains("Settings description", settings.DescriptionBox.Text);
            settings.SetLinkState(TrayMenuSection.SettingsUsers, true, true);
            Assert.Equal(Color.FromArgb(120, 205, 0), settings.ProfileLink.ForeColor);
            typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(settings.ProfileLink, new object[] { EventArgs.Empty });
            Assert.Equal(1, requests);
            settings.SetLinkState(TrayMenuSection.Recovery, false, false);
            Assert.False(settings.RecoveryLink.Enabled);
            settings.SetLinkState(TrayMenuSection.Recovery, true, false);
            Assert.True(settings.RecoveryLink.Enabled);

            settings.ApplyState(new SettingsViewState(true, false, true, string.Empty, string.Empty));
            Assert.True(settings.RecoveryPanel.Visible);
            Assert.False(settings.DescriptionBox.Visible);
            settings.RecoveryProfileComboBox.Items.Add("Select profile");
            settings.RecoveryProfileComboBox.SetSelectedIndexSilently(0);
            Assert.Equal(0, profileChanges);
            settings.RecoveryProfileComboBox.Items.Add("Alice");
            settings.RecoveryProfileComboBox.SelectedIndex = 1;
            settings.RecoveryOpenButton.Visible = true;
            settings.RecoveryOpenButton.PerformClick();
            Assert.Equal(1, profileChanges);
            Assert.Equal(1, opens);
            settings.ApplyState(new SettingsViewState(false, false, false, string.Empty, string.Empty));
            Assert.False(settings.Visible);
        });
    }

    [Fact]
    public void SidebarView_PreservesStateLayoutEventsAndUnchangedServerRows()
    {
        RunOnStaThread(() =>
        {
            using var host = new Form { ClientSize = new Size(700, 800) };
            using var sidebar = new SidebarControl();
            host.Controls.Add(sidebar);
            host.Show();
            int selections = 0, settings = 0, actions = 0, refreshes = 0;
            sidebar.ServerSelected += (_, request) => { Assert.Equal("server-one", request.NamespaceId); selections++; };
            sidebar.SettingsRequested += (_, _) => settings++;
            sidebar.PrimaryRequested += (_, _) => actions++;
            sidebar.RefreshRequested += (_, _) => refreshes++;
            sidebar.ApplyIdentity(new SidebarIdentityState("Alice", "Connected", Color.Green, Color.White, "Summary"));
            Assert.Equal("Profile: Alice", sidebar.ProfileLabel.Text);
            var statuses = new[] { new TrayServerStatus("server-one", "Database", true, true) };
            sidebar.RenderServers(statuses);
            Control row = Assert.Single(sidebar.ServerStatusPanel.Controls.Cast<Control>());
            sidebar.RenderServers(new[] { statuses[0] });
            Assert.Same(row, Assert.Single(sidebar.ServerStatusPanel.Controls.Cast<Control>()));
            typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(row, new object[] { EventArgs.Empty });
            Assert.Equal(1, selections);

            sidebar.ApplyNavigation(new SidebarNavigationState(true, false, TrayMenuSection.Login, false));
            Assert.False(sidebar.CredentialsNavButton.Enabled);
            Assert.Equal(sidebar.ServerStatusPanel.Bottom + 15, sidebar.MenuSectionLabel.Top);
            sidebar.ApplyActions(new SidebarActionState(true, true, false, false));
            Assert.Equal("Logout", sidebar.PrimaryActionButton.Text);
            Assert.True(sidebar.PrimaryActionButton.Visible);
            sidebar.PrimaryActionButton.PerformClick();
            sidebar.RefreshButton.PerformClick();
            sidebar.SettingsNavButton.PerformClick();
            Assert.Equal(new[] { 1, 1, 1 }, new[] { actions, refreshes, settings });
            sidebar.ApplyActions(new SidebarActionState(true, true, true, true));
            Assert.False(sidebar.PrimaryActionButton.Enabled);
            Assert.False(sidebar.RefreshButton.Enabled);
            sidebar.RenderServers(new[] { new TrayServerStatus("server-one", "Database", false, true) });
            Assert.True(row.IsDisposed);
        });
    }

    [Fact]
    public void DevicesView_PreservesScrollAcrossActualRowUpdates()
    {
        RunOnStaThread(() =>
        {
            using var host = new Form { ClientSize = new Size(700, 700) };
            using var devices = new DevicesControl { Visible = true };
            host.Controls.Add(devices);
            host.Show();
            devices.LayoutContent(660);
            DeviceListItem[] items = Enumerable.Range(0, 20).Select(index => new DeviceListItem
            {
                IdentityKey = index.ToString(),
                Device = new TrayApp.Models.AccountDeviceDto { DesktopDeviceId = index.ToString(), DeviceName = "Device " + index }
            }).ToArray();
            devices.Render(false, true, true, string.Empty, items, string.Empty, false);
            devices.ListPanel.AutoScrollPosition = new Point(0, 180);
            Point scroll = devices.ListPanel.AutoScrollPosition;
            Assert.True(scroll.Y < 0);
            devices.Render(false, true, true, string.Empty, items, "0", false);
            Assert.Equal(scroll, devices.ListPanel.AutoScrollPosition);
        });
    }

    [Fact]
    public void DevicesView_ReusesUnchangedRowsButInvalidatesDisplayAndDeletionContext()
    {
        RunOnStaThread(() =>
        {
            using var devices = new DevicesControl();
            DeviceListItem Item(string name, string account) => new()
            {
                IdentityKey = "device",
                SourceUrl = "https://server.example",
                Device = new TrayApp.Models.AccountDeviceDto { DesktopDeviceId = "desktop", DeviceName = name },
                NamespaceItem = new TrayApp.Models.NamespaceItemDto { AccountId = account, DesktopDeviceId = "signing-device" }
            };
            void Render(DeviceListItem item) => devices.Render(false, true, true, string.Empty, new[] { item }, string.Empty, false);
            Render(Item("Laptop", "account-one"));
            Control original = Assert.Single(devices.ListPanel.Controls.Cast<Control>());
            Render(Item("Laptop", "account-one"));
            Assert.Same(original, Assert.Single(devices.ListPanel.Controls.Cast<Control>()));
            Assert.False(original.IsDisposed);

            Render(Item("Renamed laptop", "account-one"));
            Assert.True(original.IsDisposed);
            Control renamed = Assert.Single(devices.ListPanel.Controls.Cast<Control>());
            Assert.Contains(renamed.Controls.OfType<Label>(), label => label.Text == "Renamed laptop");
            Render(Item("Renamed laptop", "account-two"));
            Assert.True(renamed.IsDisposed);
        });
    }

    [Fact]
    public void LoginView_OwnsSelectorsAndPreservesLoginAndLogoutLayout()
    {
        RunOnStaThread(() =>
        {
            using var host = new Form { ClientSize = new Size(700, 700) };
            using var login = new LoginControl { Visible = true };
            host.Controls.Add(login);
            host.Show();
            int requests = 0;
            login.LoginRequested += (_, _) => requests++;
            login.ApplyState(new LoginViewState(true, true, false, false, true, false));
            Assert.Same(login, login.ProfileSelector.Parent);
            Assert.Same(login, login.ServerSelector.Parent);
            Assert.Equal(166, login.Height);
            Assert.Equal(132, login.LoginButton.Top);
            Assert.Equal("Login", login.LoginButton.Text);
            login.LoginButton.PerformClick();
            Assert.Equal(1, requests);
            login.ApplyState(new LoginViewState(false, false, true, false, false, true));
            Assert.False(login.ProfileSelector.Visible);
            Assert.Equal(108, login.Height);
            Assert.Equal(36, login.ServerSelector.Top);
            Assert.Equal("Logout", login.LoginButton.Text);
            login.ApplyState(new LoginViewState(false, false, true, true, false, false));
            Assert.Equal("Logging out...", login.LoginButton.Text);
            Assert.False(login.LoginButton.Enabled);
        });
    }

    [Fact]
    public void ProfileView_OwnsPanelsAndForwardsActionsAcrossModes()
    {
        RunOnStaThread(() =>
        {
            using var host = new Form { ClientSize = new Size(700, 800) };
            using var profile = new ProfileControl();
            host.Controls.Add(profile);
            host.Show();
            int renames = 0, registrations = 0, backs = 0, cancellations = 0, deletions = 0, selections = 0;
            profile.RenameRequested += (_, _) => renames++;
            profile.RegistrationSubmitRequested += (_, _) => registrations++;
            profile.RegistrationBackRequested += (_, _) => backs++;
            profile.DeletionCancelRequested += (_, _) => cancellations++;
            profile.DeletionConfirmRequested += (_, _) => deletions++;
            profile.ActionChanged += (_, _) => selections++;
            profile.ShowSection(true, true, false, false, true);
            profile.NameTextBox.Text = "Profile name";
            profile.ActionComboBox.Items.Add("Rename");
            profile.ActionComboBox.SetSelectedIndexSilently(0);
            Assert.Equal(0, selections);
            profile.ActionComboBox.Items.Add("Delete");
            profile.ActionComboBox.SelectedIndex = 1;
            Assert.Equal(1, selections);
            Assert.Same(profile, profile.RenamePanel.Parent);
            Assert.Same(profile.RenamePanel, profile.NameTextBox.Parent);
            Assert.Equal(295, profile.Top + profile.RenamePanel.Top);
            Assert.True(profile.ActionPanel.Visible);
            profile.RenameSubmitButton.PerformClick();

            profile.ShowSection(true, false, true, false, true);
            Assert.True(profile.RegistrationPanel.Visible);
            Assert.False(profile.ActionPanel.Visible);
            Assert.False(profile.RenamePanel.Visible);
            Assert.Equal(152, profile.Top + profile.RegistrationPanel.Top);
            profile.RegistrationSubmitButton.PerformClick();
            profile.RegistrationBackButton.PerformClick();

            profile.ShowSection(true, false, false, true, true);
            Assert.False(profile.RegistrationPanel.Visible);
            Assert.True(profile.DeletionPanel.Visible);
            profile.DeletionCancelButton.PerformClick();
            profile.DeletionConfirmButton.PerformClick();
            Assert.Equal(new[] { 1, 1, 1, 1, 1 }, new[] { renames, registrations, backs, cancellations, deletions });

            profile.ShowSection(false, false, false, false, false);
            Assert.False(profile.Visible);
            profile.ShowSection(true, true, false, false, false);
            Assert.Equal("Profile name", profile.NameTextBox.Text);
            Assert.False(profile.RenameSubmitButton.Enabled);
        });
    }

    [Theory]
    [InlineData(true, false, false, "Continue", true)]
    [InlineData(false, false, false, "Continue", true)]
    [InlineData(true, true, false, "Generating...", false)]
    [InlineData(false, false, true, "QR ready", false)]
    public void DatabaseView_PreservesRegistrationStateAndGeometry(
        bool newServer, bool submitting, bool awaitingCompletion, string actionText, bool enabled)
    {
        RunOnStaThread(() =>
        {
            using var host = new Form { ClientSize = new Size(700, 800) };
            using var databases = new ManageDatabasesControl();
            using var qr = new Bitmap(8, 8);
            host.Controls.Add(databases);
            host.Show();
            databases.QrPictureBox.Image = qr;
            databases.ShowSection(false, true);
            databases.ApplyRegistrationState(newServer, submitting, awaitingCompletion, "Registration status");
            Assert.True(databases.RegistrationPanel.Visible);
            Assert.False(databases.ActionPanel.Visible);
            Assert.Same(databases.RegistrationPanel, databases.UrlTextBox.Parent);
            Assert.Equal(newServer, databases.NameTextBox.Visible);
            Assert.Equal(newServer ? 260 : 544, databases.UrlTextBox.Width);
            Assert.Equal(actionText, databases.SubmitButton.Text);
            Assert.Equal(enabled, databases.SubmitButton.Enabled);
            Assert.Equal("Registration status", databases.StatusLabel.Text);
            Assert.True(databases.QrPictureBox.Visible);
            Assert.Equal(232, databases.BackButton.Top);
            Assert.Equal(databases.BackButton.Right + 5, databases.SubmitButton.Left);
            Assert.Equal(270, databases.QrPictureBox.Top);
            Assert.Contains(newServer ? "Add a server:" : "Sync existing server:", databases.GuidanceBox.Text);

            databases.ShowSection(true, false);
            Assert.True(databases.ActionPanel.Visible);
            Assert.False(databases.RegistrationPanel.Visible);
            Assert.Equal(205, databases.Top + databases.ActionPanel.Top);
            databases.ShowSection(false, false);
            Assert.False(databases.Visible);
        });
    }

    [Fact]
    public void DatabaseView_ForwardsRegistrationActionsOnce()
    {
        RunOnStaThread(() =>
        {
            using var host = new Form { ClientSize = new Size(700, 800) };
            using var databases = new ManageDatabasesControl();
            host.Controls.Add(databases);
            host.Show();
            int submissions = 0, backs = 0, restarts = 0, selections = 0;
            databases.SubmitRequested += (_, _) => submissions++;
            databases.BackRequested += (_, _) => backs++;
            databases.RestartRequested += (_, _) => restarts++;
            databases.ActionChanged += (_, _) => selections++;
            databases.ShowSection(true, false);
            databases.ActionComboBox.Items.Add("Sync");
            databases.ActionComboBox.SetSelectedIndexSilently(0);
            Assert.Equal(0, selections);
            databases.ActionComboBox.Items.Add("Manage");
            databases.ActionComboBox.SelectedIndex = 1;
            databases.ShowSection(false, true);
            databases.ApplyRegistrationState(false, false, false, string.Empty);
            databases.SubmitButton.PerformClick();
            databases.BackButton.PerformClick();
            databases.RestartButton.Visible = true;
            databases.RestartButton.PerformClick();
            Assert.Equal(new[] { 1, 1, 1, 1 }, new[] { submissions, backs, restarts, selections });
        });
    }

    [Theory]
    [InlineData(true, false, false, "", "Loading registered devices...")]
    [InlineData(false, false, false, "", "Select Devices")]
    [InlineData(false, true, false, "", "Log in to one or more servers")]
    [InlineData(false, true, true, "Server unavailable", "Server unavailable")]
    public void DevicesView_RendersLoadingAndEmptyStates(bool loading, bool loaded, bool hasServers, string error, string message)
    {
        RunOnStaThread(() =>
        {
            using var devices = new DevicesControl();
            devices.Render(loading, loaded, hasServers, error, Array.Empty<DeviceListItem>(), string.Empty, false);
            Assert.Contains(devices.ListPanel.Controls.OfType<Label>(), label => label.Text.Contains(message, StringComparison.Ordinal));
            foreach (int height in new[] { 660, 900, 660 })
            {
                devices.LayoutContent(height);
                Assert.Equal(height - 15, devices.Top + devices.Height);
                Assert.Equal(190, devices.Top + devices.GuidancePanel.Top);
                Assert.Equal(15, devices.ListPanel.Top - devices.SourceComboBox.Bottom);
                Assert.Equal(devices.Height, devices.ListPanel.Bottom);
            }
        });
    }

    [Fact]
    public void DevicesView_ForwardsTypedDeletionEventsAndDisposesReplacedRows()
    {
        RunOnStaThread(() =>
        {
            using var host = new Form { ClientSize = new Size(700, 800) };
            using var devices = new DevicesControl { Visible = true };
            host.Controls.Add(devices);
            host.Show();
            var item = new DeviceListItem
            {
                IdentityKey = "desktop-key",
                SourceUrl = "https://server.example",
                Device = new TrayApp.Models.AccountDeviceDto { DesktopDeviceId = "desktop", DeviceName = "Laptop" }
            };
            var items = new[] { item };
            int confirmations = 0, cancellations = 0, deletions = 0, selections = 0;
            devices.DeletionConfirmationRequested += (_, selected) => { Assert.Same(item, selected); confirmations++; };
            devices.DeleteRequested += (_, selected) => { Assert.Same(item, selected); deletions++; };
            devices.DeletionCancelled += (_, _) => cancellations++;
            devices.SourceChanged += (_, _) => selections++;
            devices.SourceComboBox.Items.Add("All devices");
            devices.SourceComboBox.SetSelectedIndexSilently(0);
            Assert.Equal(0, selections);
            devices.SourceComboBox.Items.Add(item.SourceUrl);
            devices.SourceComboBox.SelectedIndex = 1;
            devices.Render(false, true, true, string.Empty, items, string.Empty, false);
            Control original = Assert.Single(devices.ListPanel.Controls.Cast<Control>());
            Panel actions = Assert.Single(original.Controls.OfType<Panel>());
            Assert.Single(actions.Controls.OfType<Button>()).PerformClick();
            Assert.Equal(1, confirmations);

            devices.Render(false, true, true, string.Empty, items, item.IdentityKey, false);
            Assert.True(original.IsDisposed);
            Control row = Assert.Single(devices.ListPanel.Controls.Cast<Control>());
            actions = Assert.Single(row.Controls.OfType<Panel>());
            actions.Controls.OfType<Button>().Single(button => button.Text == "Cancel").PerformClick();
            actions.Controls.OfType<Button>().Single(button => button.Text == "Delete").PerformClick();
            Assert.Equal(new[] { 1, 1, 1 }, new[] { cancellations, deletions, selections });

            devices.Render(false, true, true, string.Empty, items, item.IdentityKey, true);
            row = Assert.Single(devices.ListPanel.Controls.Cast<Control>());
            actions = Assert.Single(row.Controls.OfType<Panel>());
            Button deleting = actions.Controls.OfType<Button>().Single(button => button.Text == "Deleting");
            Assert.False(deleting.Enabled);
            deleting.PerformClick();
            Assert.Equal(1, deletions);
        });
    }

    [Fact]
    public void FaqView_PreservesAccordionExpansionAcrossRefresh()
    {
        RunOnStaThread(() =>
        {
            using var host = new Form();
            using var faq = new FaqControl { Visible = true };
            host.Controls.Add(faq);
            host.Show();
            faq.RefreshContent();
            Assert.NotEmpty(faq.Controls.Cast<Control>());
            var firstItem = Assert.IsType<Panel>(faq.Controls[0]);
            var question = Assert.Single(firstItem.Controls.OfType<Button>());
            string questionText = question.Text;
            int closedHeight = firstItem.Height;

            question.PerformClick();
            Assert.True(faq.Controls[0].Height > closedHeight);
            faq.RefreshContent();
            firstItem = Assert.IsType<Panel>(faq.Controls[0]);
            Assert.Equal(questionText, Assert.Single(firstItem.Controls.OfType<Button>()).Text);
            Assert.True(firstItem.Height > closedHeight);

            Assert.Single(firstItem.Controls.OfType<Button>()).PerformClick();
            Assert.Equal(closedHeight, faq.Controls[0].Height);
        });
    }

    [Fact]
    public void CollapseIcon_PreservesDefaultStateAndTransparentBackground()
    {
        RunOnStaThread(() =>
        {
            using var icon = new PanelCollapseIcon();
            Assert.True(icon.Collapsed);
            Assert.Equal(Color.Transparent, icon.BackColor);
            Assert.Equal(new Size(28, 28), icon.Size);
            Assert.Equal("Show credential list", icon.AccessibleName);

            icon.Collapsed = false;
            Assert.Equal("Hide credential list", icon.AccessibleName);
            Assert.Equal(Color.Transparent, icon.BackColor);
        });
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, true, true)]
    public void CredentialWorkspace_PreservesEntryAndGroupActionStates(
        bool creating, bool canDelete, bool confirmingDeletion, bool loading)
    {
        RunOnStaThread(() =>
        {
            using var host = new Form();
            using var workspace = new CredentialWorkspaceControl { Visible = true };
            host.Controls.Add(workspace);
            host.Show();
            workspace.ApplyEntryState(creating, canDelete, confirmingDeletion, loading, "Entry status");
            workspace.ApplyGroupState(creating, canDelete, confirmingDeletion, loading, "Group status");
            workspace.ShowEditor(false, true, !confirmingDeletion, 900);

            Assert.Equal(confirmingDeletion ? "Cancel" : creating ? "Save new item" : "Save changes", workspace.SaveButton.Text);
            Assert.Equal(canDelete, workspace.DeleteButton.Visible);
            Assert.Equal(canDelete && !loading, workspace.DeleteButton.Enabled);
            Assert.Equal(confirmingDeletion ? 0 : canDelete ? 112 : 0, workspace.SaveButton.Left);
            Assert.Equal("Entry status", workspace.StatusLabel.Text);
            Assert.Equal(!confirmingDeletion, workspace.BackButton.Visible);

            workspace.ShowEditor(true, false, !confirmingDeletion, 900);
            Assert.Equal(confirmingDeletion ? "Cancel" : creating ? "Save group" : "Save changes", workspace.GroupSaveButton.Text);
            Assert.Equal(canDelete && !loading, workspace.GroupDeleteButton.Enabled);
            Assert.Equal(!creating, workspace.GroupDeleteButton.Visible);
            Assert.Equal(!creating && !confirmingDeletion, workspace.GroupDeletionInfoPanel.Visible);
            Assert.Equal("Group status", workspace.GroupStatusLabel.Text);
        });
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void CredentialWorkspace_PreservesIndependentEntryAvailability(bool enabled, bool assignmentEnabled, bool saveEnabled)
    {
        RunOnStaThread(() =>
        {
            using var workspace = new CredentialWorkspaceControl();
            workspace.LabelTextBox.Text = "Example";
            workspace.PasswordTextBox.Text = "Secret";
            workspace.SetEntryAvailability(enabled, assignmentEnabled, saveEnabled);
            Assert.Equal(enabled, workspace.LabelTextBox.Enabled);
            Assert.Equal(enabled, workspace.PasswordTextBox.Enabled);
            Assert.Equal(assignmentEnabled, workspace.AssignedGroupComboBox.Enabled);
            Assert.Equal(saveEnabled, workspace.SaveButton.Enabled);

            workspace.ClearEntry();
            Assert.Empty(workspace.LabelTextBox.Text);
            Assert.Empty(workspace.PasswordTextBox.Text);
        });
    }

    [Fact]
    public void CredentialWorkspace_OwnsEditorsAndPreservesVerticalLayout()
    {
        RunOnStaThread(() =>
        {
            using var workspace = new CredentialWorkspaceControl();
            Assert.Same(workspace, workspace.EntryEditorPanel.Parent);
            Assert.Same(workspace, workspace.GroupEditorPanel.Parent);
            Assert.Same(workspace.EntryEditorPanel, workspace.PasswordTextBox.Parent);
            Assert.Same(workspace.GroupEditorPanel, workspace.ManagedGroupComboBox.Parent);
            Assert.True(workspace.PasswordTextBox.UseSystemPasswordChar);
            Assert.Equal(new Size(578, 414), workspace.Size);

            foreach (int availableHeight in new[] { 660, 900, 660 })
            {
                workspace.LayoutEditor(availableHeight);
                Assert.Equal(availableHeight - workspace.Top - 15, workspace.Height);
                Assert.Equal(workspace.SaveButton.Top, workspace.DeleteButton.Top);
                Assert.Equal(16, workspace.SaveButton.Top - workspace.DescriptionTextBox.Bottom);
                Assert.Equal(12, workspace.StatusLabel.Top - workspace.SaveButton.Bottom);
                Assert.Equal(547, workspace.EntryEditorPanel.Width);
            }
        });
    }

    [Fact]
    public void CredentialWorkspace_ForwardsEditorActionsOnce()
    {
        RunOnStaThread(() =>
        {
            using var host = new Form();
            using var workspace = new CredentialWorkspaceControl { Visible = true };
            host.Controls.Add(workspace);
            host.Show();
            int saves = 0;
            int deletes = 0;
            int inputChanges = 0;
            workspace.SaveRequested += (_, _) => saves++;
            workspace.DeleteRequested += (_, _) => deletes++;
            workspace.EntryInputChanged += (_, _) => inputChanges++;

            workspace.LabelTextBox.Text = "Example";
            workspace.SaveButton.PerformClick();
            workspace.DeleteButton.PerformClick();

            Assert.Equal(1, saves);
            Assert.Equal(1, deletes);
            Assert.Equal(1, inputChanges);
        });
    }

    [Fact]
    public void Recovery_ContentRemainsInsideSettingsContainerAfterNavigationAndResize()
    {
        RunOnStaThread(() =>
        {
            var form = (Form1)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Form1));
            using var detail = new Panel { Size = new Size(643, 600) };
            using var settings = new SettingsControl { Visible = true };
            FlowLayoutPanel navigation = settings.NavigationPanel;
            RichTextBox settingsDescription = settings.DescriptionBox;
            Panel recovery = settings.RecoveryPanel;
            Label profileLabel = settings.RecoveryProfileLabel;
            TrayDropdown profiles = settings.RecoveryProfileComboBox;
            RichTextBox description = settings.RecoveryDescriptionBox;
            Label status = settings.RecoveryStatusLabel;
            Button open = settings.RecoveryOpenButton;
            var controller = new TrayMenuController((_, _) => { }, () => { });
            void Set(string field, object value) => typeof(Form1)
                .GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(form, value);
            void Invoke(string method) => typeof(Form1)
                .GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(form, null);

            Set("_quickPanelDetailPanel", detail);
            Set("_settingsView", settings);
            Set("_selectedRecoveryProfileUserId", string.Empty);
            Set("_trayMenuController", controller);
            detail.Controls.Add(settings);
            settings.Controls.Add(navigation);
            settings.Controls.Add(settingsDescription);
            settings.Controls.Add(recovery);
            Invoke("InitializeRecoveryPanel");

            controller.SetActiveSection(TrayMenuSection.SettingsUsers);
            Invoke("LayoutSettingsOverview");
            Assert.Equal(navigation.Height, settings.Height);

            controller.SetActiveSection(TrayMenuSection.Recovery);
            recovery.Visible = true;
            Invoke("LayoutSettingsOverview");
            Assert.True(profiles.Visible);
            Assert.False(description.Visible);
            Assert.True(settings.ClientRectangle.Contains(recovery.Bounds));
            Assert.True(recovery.ClientRectangle.Contains(profiles.Bounds));

            Type optionType = typeof(Form1).GetNestedType("SelectionOption",
                System.Reflection.BindingFlags.NonPublic)!;
            profiles.Items.Add(Activator.CreateInstance(optionType, "Alice", "alice")!);
            profiles.SelectedIndex = 0;
            Assert.True(description.Visible);
            Assert.True(open.Visible);
            Assert.Equal("Open recovery", open.Text);
            Assert.Contains("damaged or lost", description.Text);
            Assert.Contains("email address", description.Text);
            Assert.Contains("new mobile phone", description.Text);

            foreach (int height in new[] { 720, 440 })
            {
                detail.Height = height;
                Invoke("LayoutSettingsOverview");
                Assert.Equal(height - settings.Top - 15, settings.Height);
                Assert.True(settings.ClientRectangle.Contains(recovery.Bounds));
                Assert.True(recovery.ClientRectangle.Contains(profiles.Bounds));
                Assert.True(recovery.ClientRectangle.Contains(open.Bounds));
                Assert.True(recovery.ClientRectangle.Contains(description.Bounds));
                Assert.Equal(recovery.Height, description.Bottom);
            }

            using var card = new RecoveryControl(new TrayApp.Models.DesktopRegistration());
            typeof(Form1).GetMethod("ShowRecoveryCard",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(form, new object[] { card });
            Assert.Same(recovery, card.Parent);
            Assert.Equal(DockStyle.Fill, card.Dock);
            Assert.IsNotAssignableFrom<Form>(card);
            Assert.False(profiles.Enabled);
            Assert.False(open.Enabled);
            foreach (int height in new[] { 720, 440 })
            {
                detail.Height = height;
                Invoke("LayoutSettingsOverview");
                recovery.PerformLayout();
                Assert.Equal(recovery.ClientRectangle, card.Bounds);
            }
            var back = card.Controls.Find("RecoveryBack", true).Single();
            typeof(Control).GetMethod("OnClick",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(back, new object[] { EventArgs.Empty });
            Assert.True(card.IsDisposed);
            Assert.DoesNotContain(card, recovery.Controls.Cast<Control>());
            Assert.True(open.Visible);
            Assert.True(profiles.Enabled);
            Assert.True(open.Enabled);
        });
    }

    [Fact]
    public void Brand_ClickOpensWebsiteUsingDefaultBrowser()
    {
        RunOnStaThread(() =>
        {
            var requests = new List<System.Diagnostics.ProcessStartInfo>();
            using var brand = new Pa55wordBrand(requests.Add);
            var wordmark = Assert.Single(brand.Controls.OfType<PictureBox>());
            var onClick = typeof(Control).GetMethod("OnClick",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

            onClick.Invoke(wordmark, new object[] { EventArgs.Empty });
            onClick.Invoke(brand, new object[] { EventArgs.Empty });

            Assert.Equal(2, requests.Count);
            Assert.All(requests, request =>
            {
                Assert.Equal("https://pa55word.com", request.FileName);
                Assert.True(request.UseShellExecute);
            });
            Assert.Equal(Cursors.Hand, wordmark.Cursor);
        });
    }

    [Fact]
    public void Brand_LoadsEmbeddedWordmarkWithoutTagline()
    {
        RunOnStaThread(() =>
        {
            using var brand = new Pa55wordBrand();
            var wordmark = Assert.Single(brand.Controls.OfType<PictureBox>());
            Assert.Empty(brand.Controls.OfType<Label>());

            Assert.NotNull(wordmark.Image);
            Assert.Equal(new Size(1000, 240), wordmark.Image.Size);
            Assert.Equal(PictureBoxSizeMode.Zoom, wordmark.SizeMode);
            Assert.Equal(new Size(140, 34), brand.Size);
            Assert.Equal(DockStyle.Fill, wordmark.Dock);
            Assert.False(brand.TabStop);
            using var bitmap = new Bitmap(wordmark.Image);
            Assert.Equal(Color.FromArgb(120, 205, 0).ToArgb(), bitmap.GetPixel(405, 10).ToArgb());
            Assert.Equal(0, bitmap.GetPixel(405, 25).A);
            Assert.Equal(0, bitmap.GetPixel(0, 0).A);
        });
    }

    [Theory]
    [InlineData(false, (int)TrayMenuSection.Login)]
    [InlineData(true, (int)TrayMenuSection.Credentials)]
    public void Startup_SelectsSectionForActiveNamespace(bool authorized, int expected)
    {
        var controller = new TrayMenuController((_, _) => { }, () => { });
        controller.SetActiveSection(TrayMenuSection.Settings);

        controller.SetInitialSection(authorized);

        Assert.Equal((TrayMenuSection)expected, controller.ActiveSection);
    }

    [Fact]
    public void CredentialList_ShowsAllGroupsAndSelectsEntriesByIdWithoutFeedback()
    {
        RunOnStaThread(() =>
        {
            using var panel = new CredentialListPanel();
            var entries = new[]
            {
                new CredentialEntryListItem(1, "Same label", "group-a", "Group A", "", "", ""),
                new CredentialEntryListItem(2, "Same label", "group-b", "Group B", "", "", ""),
                new CredentialEntryListItem(3, "Ungrouped entry", "", "", "", "", "")
            };
            ListBox list = Assert.Single(panel.Controls.OfType<ListBox>());
            var selections = new List<CredentialEntryListItem>();
            panel.EntrySelected += selections.Add;

            panel.UpdateEntries(entries, "2", false, "");

            Assert.Equal(3, list.Items.Count);
            Assert.Same(entries[1], list.SelectedItem);
            Assert.Empty(selections);

            list.SelectedIndex = 2;

            Assert.Same(entries[2], Assert.Single(selections));
            panel.UpdateEntries(entries, "1", false, "");
            Assert.Equal(3, list.Items.Count);
            Assert.Same(entries[0], list.SelectedItem);
            Assert.Single(selections);

            var updatedEntry = entries[1] with { Label = "Renamed credential" };
            panel.UpdateEntries(new[] { updatedEntry }, "2", false, "");
            Assert.Same(updatedEntry, Assert.Single(list.Items.Cast<CredentialEntryListItem>()));
            Assert.Same(updatedEntry, list.SelectedItem);
            Assert.Single(selections);

            panel.UpdateEntries(Array.Empty<CredentialEntryListItem>(), "", false, "");
            Assert.Empty(list.Items.Cast<CredentialEntryListItem>());
            Assert.Equal(-1, list.SelectedIndex);
        });
    }

    [Fact]
    public void CredentialList_DisablesSelectionWhileLoadingOrFailed()
    {
        RunOnStaThread(() =>
        {
            using var panel = new CredentialListPanel();
            ListBox list = Assert.Single(panel.Controls.OfType<ListBox>());

            panel.UpdateEntries(Array.Empty<CredentialEntryListItem>(), "", true, "");
            Assert.False(list.Enabled);
            Assert.Contains(panel.Controls.OfType<Label>(), label => label.Text == "Loading credentials...");

            panel.UpdateEntries(Array.Empty<CredentialEntryListItem>(), "", false, "Server unavailable");
            Assert.False(list.Enabled);
            Assert.Contains(panel.Controls.OfType<Label>(), label => label.Text == "Server unavailable");

            panel.UpdateEntries(Array.Empty<CredentialEntryListItem>(), "", false, "");
            Assert.True(list.Enabled);
            Assert.Contains(panel.Controls.OfType<Label>(), label => label.Text == "No credentials available.");
        });
    }

    [Fact]
    public void CredentialList_MatchesDetailPanelHeightAndKeepsEditorWidth()
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            using var content = new Panel();
            using var navigation = new Panel { Dock = DockStyle.Left, Width = 177 };
            using var details = new Panel { Dock = DockStyle.Fill };
            using var list = new CredentialListPanel();
            content.Controls.Add(details);
            content.Controls.Add(list);
            content.Controls.Add(navigation);
            MainWindowPresentation.AttachContent(window, content);
            MainWindowPresentation.Configure(window, showCredentialList: true);
            window.Show();
            window.PerformLayout();
            content.PerformLayout();

            Assert.Equal(details.Height, list.Height);
            Assert.Equal(list.Right, details.Left);
            Assert.True(details.Width >= 643);

            window.ClientSize = new Size(window.ClientSize.Width, 900);
            window.PerformLayout();
            content.PerformLayout();
            Assert.Equal(900 - WindowHeader.HeaderHeight, list.Height);
            Assert.Equal(details.Height, list.Height);

            list.Visible = false;
            MainWindowPresentation.Configure(window);
            window.ClientSize = MainWindowPresentation.MinimumClientSize;
            content.PerformLayout();
            Assert.Equal(643, details.Width);
        });
    }

    [Fact]
    public void Content_IsHostedOnFormAndFollowsClientSize()
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            using var content = new Panel();

            MainWindowPresentation.AttachContent(window, content);
            MainWindowPresentation.Configure(window);
            window.Show();
            window.PerformLayout();

            Assert.Same(window, content.Parent);
            Assert.Equal(DockStyle.Fill, content.Dock);
            Assert.Equal(new Size(window.ClientSize.Width,
                window.ClientSize.Height - WindowHeader.HeaderHeight), content.Size);
            Assert.Equal(WindowHeader.HeaderHeight, content.Top);
            var header = Assert.Single(window.Controls.OfType<WindowHeader>());
            var headerLogo = Assert.Single(header.Controls.OfType<Pa55wordBrand>());
            Assert.Equal(new Size(140, 34), headerLogo.Size);
            Assert.Equal(Color.FromArgb(31, 33, 57), header.BackColor);
            var headerImage = Assert.Single(headerLogo.Controls.OfType<PictureBox>()).Image;
            Assert.NotNull(headerImage);
            using var headerBitmap = new Bitmap(headerImage);
            Assert.Equal(Color.FromArgb(247, 248, 250).ToArgb(), headerBitmap.GetPixel(40, 110).ToArgb());
            Assert.Equal(Color.FromArgb(120, 205, 0).ToArgb(),
                headerBitmap.GetPixel(405, 10).ToArgb());
            Assert.Equal(0, headerBitmap.GetPixel(405, 25).A);
            Assert.DoesNotContain(header.Controls.OfType<Button>(), button => button.Text == "+");
            Assert.Equal(new[] { "Minimize", "Close" },
                header.Controls.OfType<Button>().Select(button => button.AccessibleName));
            var minimize = Assert.Single(header.Controls.OfType<Button>(),
                button => button.AccessibleName == "Minimize");
            using var minimizeBitmap = new Bitmap(minimize.Width, minimize.Height);
            minimize.DrawToBitmap(minimizeBitmap, minimize.ClientRectangle);
            int minimizeCenterX = minimize.ClientSize.Width / 2;
            int minimizeCenterY = minimize.ClientSize.Height / 2;
            Assert.Equal(minimize.ForeColor.ToArgb(),
                minimizeBitmap.GetPixel(minimizeCenterX, minimizeCenterY).ToArgb());
            Assert.Equal(minimize.BackColor.ToArgb(),
                minimizeBitmap.GetPixel(minimizeCenterX, minimizeCenterY + 5).ToArgb());
            Assert.Contains(header.Controls.OfType<Label>(),
                label => label.Text == "It just feels right | Effortless by Design" &&
                    label.ForeColor == Color.FromArgb(247, 248, 250));
            var separator = Assert.Single(header.Controls.OfType<Panel>());
            Assert.Equal(Color.White, separator.BackColor);
            Assert.Equal(1, separator.Height);
            Assert.Equal(FormBorderStyle.Sizable, window.FormBorderStyle);
            Assert.False(window.MaximizeBox);
            Assert.True(window.MinimizeBox);
            Assert.True(window.ShowInTaskbar);
            Assert.Equal("pa55word It just feels right | Effortless by Design", window.Text);
            Assert.NotNull(window.Icon);
            Assert.Equal(window.Size.Width, window.MaximumSize.Width);
            Assert.Equal(int.MaxValue, window.MaximumSize.Height);
            Assert.True(window.ClientSize.Width >= MainWindowPresentation.MinimumClientSize.Width);
            Assert.True(window.ClientSize.Height >= MainWindowPresentation.MinimumClientSize.Height);

            int fixedWidth = window.ClientSize.Width;
            window.ClientSize = new Size(fixedWidth, 800);
            window.PerformLayout();

            Assert.Equal(new Size(fixedWidth, 800 - WindowHeader.HeaderHeight), content.Size);
            MainWindowPresentation.Configure(window);
            Assert.Equal(new Size(fixedWidth, 800), window.ClientSize);
        });
    }

    [Fact]
    public void Configure_RestoresTheRequiredFixedWidth()
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            MainWindowPresentation.Configure(window);

            window.MinimumSize = Size.Empty;
            window.MaximumSize = Size.Empty;
            window.ClientSize = new Size(1200, 800);
            MainWindowPresentation.Configure(window);

            Assert.Equal(MainWindowPresentation.MinimumClientSize.Width, window.ClientSize.Width);
            Assert.Equal(window.Size.Width, window.MinimumSize.Width);
            Assert.Equal(window.Size.Width, window.MaximumSize.Width);
            Assert.Equal(800, window.ClientSize.Height);
        });
    }

    [Fact]
    public void HideAndReopen_PreservesContentAndWindowSize()
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            using var content = new Panel();
            using var input = new TextBox { Text = "unsaved value" };
            content.Controls.Add(input);
            MainWindowPresentation.AttachContent(window, content);
            MainWindowPresentation.Configure(window);
            int fixedWidth = window.ClientSize.Width;
            window.ClientSize = new Size(fixedWidth, 800);
            MainWindowPresentation.Show(window);

            MainWindowPresentation.Hide(window);

            Assert.False(window.Visible);
            Assert.False(window.ShowInTaskbar);
            Assert.False(window.IsDisposed);

            MainWindowPresentation.Show(window);

            Assert.True(window.Visible);
            Assert.True(window.ShowInTaskbar);
            Assert.Equal(new Size(fixedWidth, 800), window.ClientSize);
            Assert.Equal("unsaved value", input.Text);
        });
    }

    [Fact]
    public void RegistrationTransition_RestoresNormalWindowAndCanReturnToResizableLayout()
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            MainWindowPresentation.Configure(window);
            MainWindowPresentation.Show(window);
            window.WindowState = FormWindowState.Maximized;

            MainWindowPresentation.ConfigureRegistration(window);

            Assert.Equal(FormWindowState.Normal, window.WindowState);
            Assert.Equal(new Size(800, 450), window.ClientSize);
            Assert.Equal(FormBorderStyle.FixedSingle, window.FormBorderStyle);
            Assert.False(window.MaximizeBox);
            Assert.Equal(Size.Empty, window.MaximumSize);
            Assert.True(window.ShowInTaskbar);
            Assert.Equal("pa55word It just feels right | Effortless by Design Registration", window.Text);
            Assert.NotNull(window.Icon);

            MainWindowPresentation.Configure(window);

            Assert.Equal(FormBorderStyle.Sizable, window.FormBorderStyle);
            Assert.False(window.MaximizeBox);
            Assert.True(window.ClientSize.Width >= MainWindowPresentation.MinimumClientSize.Width);
            Assert.True(window.ClientSize.Height >= MainWindowPresentation.MinimumClientSize.Height);
        });
    }

    [Fact]
    public void Reopen_RestoresMinimizedWindow()
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            MainWindowPresentation.Configure(window);
            MainWindowPresentation.Show(window);
            window.WindowState = FormWindowState.Minimized;

            Assert.True(window.ShowInTaskbar);
            MainWindowPresentation.Show(window);

            Assert.True(window.Visible);
            Assert.Equal(FormWindowState.Normal, window.WindowState);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigureBeforeFirstShow_UsesRequiredWidthImmediately(bool showCredentialList)
    {
        RunOnStaThread(() =>
        {
            using var window = new Form { WindowState = FormWindowState.Minimized, Opacity = 0 };
            _ = window.Handle;
            int expectedWidth = MainWindowPresentation.MinimumClientSize.Width +
                (showCredentialList ? CredentialListPanel.PanelWidth : 0);

            MainWindowPresentation.Configure(window, showCredentialList);
            MainWindowPresentation.Show(window);

            Assert.Equal(1, window.Opacity);
            Assert.Equal(expectedWidth, window.ClientSize.Width);
            Assert.Equal(window.Width, window.MinimumSize.Width);
            Assert.Equal(window.Width, window.MaximumSize.Width);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigureWhileMinimized_ReopensAtRequiredWidth(bool showCredentialList)
    {
        RunOnStaThread(() =>
        {
            using var window = new Form();
            MainWindowPresentation.Configure(window, showCredentialList);
            MainWindowPresentation.Show(window);
            int expectedWidth = window.ClientSize.Width;
            window.ClientSize = new Size(expectedWidth, 800);
            window.WindowState = FormWindowState.Minimized;
            MainWindowPresentation.Hide(window);

            MainWindowPresentation.Configure(window, showCredentialList);
            MainWindowPresentation.Configure(window, showCredentialList);
            Assert.Equal(FormWindowState.Minimized, window.WindowState);
            Assert.False(window.Visible);
            MainWindowPresentation.Show(window);

            Assert.Equal(FormWindowState.Normal, window.WindowState);
            Assert.Equal(new Size(expectedWidth, 800), window.ClientSize);
            Assert.Equal(window.Width, window.MinimumSize.Width);
            Assert.Equal(window.Width, window.MaximumSize.Width);
        });
    }

    private static void RunOnStaThread(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The window test did not finish in time.");
        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
