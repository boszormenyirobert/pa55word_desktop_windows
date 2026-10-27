using TrayApp.TrayMenu.Presenters;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;
using TrayApp.TrayMenu.Controls;

namespace TrayApp
{
    public partial class Form1
    {
        private readonly CredentialSelectionControl _credentialSelection = new();
        private readonly CredentialWorkspaceControl _credentialWorkspace = new();
        private readonly CredentialListPanel _quickPanelCredentialListPanel = new();
        private readonly PanelCollapseIcon _quickPanelCredentialListCollapseButton = new();
        private bool _credentialListCollapsed = true;
        private bool _updatingCredentialWorkspaceControls;

        private void RefreshCredentialQuickPanelState()
        {
            if (!_isDeviceRegistered || _trayMenuController?.ActiveSection != TrayMenuSection.Credentials)
            {
                return;
            }

            GetActiveTraySelection(out DesktopRegistration? registration, out NamespaceItemDto? activeNamespace);
            TrayMenuViewState viewState = BuildQuickPanelViewState(
                registration,
                activeNamespace,
                ResolveTrayStatus().StatusText);

            _quickPanelDetailEyebrowLabel.Text = viewState.Eyebrow;
            _quickPanelDetailTitleLabel.Text = viewState.Title;
            _quickPanelDetailDescriptionLabel.Text = viewState.Description;
            _quickPanelInsightTitleLabel.Text = viewState.InsightTitle;
            _quickPanelInsightBodyLabel.Text = viewState.InsightBody;

            if (!string.IsNullOrWhiteSpace(viewState.AccountMetricOverride))
            {
                _quickPanelAccountValueLabel.Text = viewState.AccountMetricOverride;
            }

            if (!string.IsNullOrWhiteSpace(viewState.ConnectionMetricOverride))
            {
                _quickPanelConnectionValueLabel.Text = viewState.ConnectionMetricOverride;
            }

            if (!string.IsNullOrWhiteSpace(viewState.UpdatedMetricOverride))
            {
                _quickPanelLastUpdatedValueLabel.Text = viewState.UpdatedMetricOverride;
            }

            RenderCredentialWorkspace();
        }

        private void UpdateCredentialWorkspacePresentation(bool credentialsLayout)
        {
            _quickPanelMetricsCard.Visible = !credentialsLayout;
            _quickPanelInsightCard.Visible = !credentialsLayout;
            _credentialWorkspace.Visible = credentialsLayout;
            _quickPanelCredentialListPanel.Visible = credentialsLayout && !_credentialListCollapsed;
            _quickPanelCredentialListCollapseButton.Visible = credentialsLayout;
            _quickPanelCredentialListCollapseButton.Collapsed = _credentialListCollapsed;
            _quickPanelToolTip.SetToolTip(_quickPanelCredentialListCollapseButton,
                _quickPanelCredentialListCollapseButton.AccessibleName);
            LayoutCredentialListCollapseButton();

            if (!_credentialListCollapsed)
            {
                _quickPanelCredentialListPanel.Width = CredentialListPanel.PanelWidth;
            }

            MainWindowPresentation.Configure(this, showCredentialList: credentialsLayout && !_credentialListCollapsed);
            LayoutMainWindowContent();
        }

        private void quickPanelCredentialListCollapseButton_Click(object? sender, EventArgs e)
        {
            _credentialListCollapsed = !_credentialListCollapsed;
            UpdateCredentialWorkspacePresentation(credentialsLayout: true);
        }

        private void LayoutCredentialListCollapseButton()
        {
            int boundary = _quickPanelCredentialListPanel.Visible
                ? _quickPanelCredentialListPanel.Right
                : _quickPanelDividerPanel.Right;
            _quickPanelCredentialListCollapseButton.Location = new Point(
                boundary,
                Math.Max(0, (_quickPanelContainer.Height - _quickPanelCredentialListCollapseButton.Height) / 2));
            _quickPanelCredentialListCollapseButton.BringToFront();
        }

        private void RenderCredentialWorkspace()
        {
            _updatingCredentialWorkspaceControls = true;

            try
            {
                bool showGroupManager = _credentialsPresenter.WorkspaceMode is CredentialWorkspaceMode.ManageGroup or CredentialWorkspaceMode.CreateGroup;
                bool showCreateEntry = _credentialsPresenter.WorkspaceMode == CredentialWorkspaceMode.CreateEntry;
                bool showCreateGroup = _credentialsPresenter.WorkspaceMode == CredentialWorkspaceMode.CreateGroup;
                bool showEntryEditor = showCreateEntry ||
                    (!showGroupManager && !string.IsNullOrWhiteSpace(_credentialsPresenter.SelectedEntryId));
                bool canDeleteCredential = !showGroupManager && !showCreateEntry && _credentialsPresenter.GetSelectedEntry() != null;
                bool showCredentialReadEditDeleteMode = canDeleteCredential;
                bool showCredentialDeletionConfirmation = canDeleteCredential &&
                    IsDeletionConfirmationPending(CredentialDeletionConfirmation.Credential);
                bool canDeleteGroup = showGroupManager && !showCreateGroup && _credentialsPresenter.Groups.Any(group =>
                    string.Equals(group.GroupId, _credentialsPresenter.SelectedManagedGroupId, StringComparison.Ordinal));
                bool showGroupDeletionConfirmation = canDeleteGroup &&
                    IsDeletionConfirmationPending(CredentialDeletionConfirmation.Group);

                _quickPanelCredentialListPanel.UpdateEntries(
                    _credentialsPresenter.Entries,
                    _credentialsPresenter.SelectedEntryId,
                    _credentialsPresenter.IsLoading,
                    _credentialsPresenter.ErrorMessage);

                _quickPanelDetailTitleLabel.Text = _credentialsPresenter.WorkspaceMode switch
                {
                    CredentialWorkspaceMode.ManageGroup => "Groups - Edit",
                    CredentialWorkspaceMode.CreateGroup => "Groups - Create",
                    CredentialWorkspaceMode.CreateEntry => "Credentials - Create",
                    _ => showCredentialReadEditDeleteMode
                        ? "Credentials - Read / Edit / Delete"
                        : "Credentials"
                };
                _quickPanelDetailDescriptionLabel.Text = _credentialsPresenter.WorkspaceMode switch
                {
                    CredentialWorkspaceMode.ManageGroup => "Select a credential group to edit its name.",
                    CredentialWorkspaceMode.CreateGroup => "Create a credential group for the active server.",
                    CredentialWorkspaceMode.CreateEntry => "Create a credential for the active server.",
                    _ => _quickPanelDetailDescriptionLabel.Text
                };
                ApplyCredentialSelectionCardMode(showGroupManager, showCreateGroup, showCreateEntry);
                _credentialWorkspace.ApplyEntryState(
                    showCreateEntry, canDeleteCredential, showCredentialDeletionConfirmation,
                    _credentialsPresenter.IsLoading, _credentialsPresenter.EntryStatusMessage);
                _credentialWorkspace.ApplyGroupState(
                    showCreateGroup, canDeleteGroup, showGroupDeletionConfirmation,
                    _credentialsPresenter.IsLoading, _credentialsPresenter.GroupStatusMessage);
                _credentialWorkspace.ShowEditor(
                    showGroupManager, showEntryEditor,
                    (showGroupManager || showEntryEditor) && !showCredentialDeletionConfirmation && !showGroupDeletionConfirmation,
                    _quickPanelDetailPanel.Height);

                if (showGroupManager)
                {
                    PopulateManagedGroupEditor();
                }
                else if (showCreateEntry)
                {
                    PopulateCredentialAssignedGroupSelector();
                }
                else if (showEntryEditor)
                {
                    PopulateSelectedCredentialDetail();
                }
            }
            finally
            {
                _updatingCredentialWorkspaceControls = false;
            }
        }

        private void ApplyCredentialSelectionCardMode(bool showGroupManager, bool showCreateGroup, bool showCreateEntry)
        {
            if (_trayMenuController?.ActiveSection != TrayMenuSection.Credentials)
            {
                return;
            }
            bool selectors = !showGroupManager && !showCreateEntry;
            _credentialWorkspace.Location = new Point(32, selectors ? 231 : 137);
            _credentialSelection.ApplyMode(showGroupManager, showCreateGroup, showCreateEntry,
                _credentialsPresenter.IsLoading, _credentialWorkspace.GroupEditorPanel);
            _quickPanelSelectionCard.Visible = false;
        }

        private void PopulateCredentialAssignedGroupSelector()
        {
            string currentSelection = _credentialWorkspace.AssignedGroupComboBox.SelectedItem is SelectionOption option
                ? option.Value
                : string.Empty;

            _credentialWorkspace.AssignedGroupComboBox.Items.Clear();
            _credentialWorkspace.AssignedGroupComboBox.Items.Add(new SelectionOption("No group", string.Empty));

            foreach (CredentialGroupDto group in _credentialsPresenter.Groups)
            {
                _credentialWorkspace.AssignedGroupComboBox.Items.Add(new SelectionOption(group.GroupName, group.GroupId));
            }

            string preferredSelection = _credentialsPresenter.WorkspaceMode == CredentialWorkspaceMode.CreateEntry
                ? (!string.IsNullOrWhiteSpace(_credentialsPresenter.SelectedGroupId) ? _credentialsPresenter.SelectedGroupId : currentSelection)
                : currentSelection;

            SelectComboBoxValue(_credentialWorkspace.AssignedGroupComboBox, preferredSelection);
        }

        private void ApplyCredentialDetailToEditor(CredentialEntryDetail? selectedEntry)
        {
            PopulateCredentialAssignedGroupSelector();
            _updatingCredentialWorkspaceControls = true;

            try
            {
                _credentialWorkspace.LabelTextBox.Text = selectedEntry?.Label ?? string.Empty;
                _credentialWorkspace.DomainTextBox.Text = selectedEntry?.Registry.Domain ?? string.Empty;
                _credentialWorkspace.UsernameTextBox.Text = selectedEntry?.Registry.Username ?? string.Empty;
                _credentialWorkspace.PasswordTextBox.Text = selectedEntry?.Registry.Password ?? string.Empty;
                _credentialWorkspace.DescriptionTextBox.Text = selectedEntry?.Registry.Description ?? string.Empty;
                SetCredentialPasswordVisible(false);

                SelectComboBoxValue(_credentialWorkspace.AssignedGroupComboBox, selectedEntry?.GroupId ?? string.Empty);

                bool hasSelection = selectedEntry != null;
                bool canSave = hasSelection && !_credentialsPresenter.IsLoading;
                _credentialWorkspace.SetEntryAvailability(hasSelection, canSave, canSave);

                if (string.IsNullOrWhiteSpace(_credentialsPresenter.EntryStatusMessage))
                {
                    _credentialWorkspace.StatusLabel.Text = hasSelection
                        ? "Only the selected entry is decrypted on demand."
                        : "Select entries to decrypt and view the chosen credential.";
                }
            }
            finally
            {
                _updatingCredentialWorkspaceControls = false;
            }
        }

        private void ResetCredentialCreateEditor()
        {
            PopulateCredentialAssignedGroupSelector();
            _credentialWorkspace.ClearEntry();
            SetCredentialPasswordVisible(false);
            SelectComboBoxValue(_credentialWorkspace.AssignedGroupComboBox, _credentialsPresenter.SelectedGroupId);

            _credentialWorkspace.SetEntryAvailability(true, !_credentialsPresenter.IsLoading, true);

            if (string.IsNullOrWhiteSpace(_credentialsPresenter.EntryStatusMessage))
            {
                _credentialWorkspace.StatusLabel.Text = "Create a new credential for the active server. The current group filter becomes the default group assignment.";
            }
        }

        private void PopulateManagedGroupEditor()
        {
            _credentialWorkspace.ManagedGroupComboBox.Items.Clear();
            foreach (CredentialGroupDto group in _credentialsPresenter.Groups)
            {
                _credentialWorkspace.ManagedGroupComboBox.Items.Add(new SelectionOption(group.GroupName, group.GroupId));
            }

            if (string.IsNullOrWhiteSpace(_credentialsPresenter.SelectedManagedGroupId))
            {
                _credentialsPresenter.SelectedManagedGroupId = _credentialsPresenter.Groups.FirstOrDefault()?.GroupId ?? string.Empty;
            }

            SelectComboBoxValue(_credentialWorkspace.ManagedGroupComboBox, _credentialsPresenter.SelectedManagedGroupId);

            CredentialGroupDto? selectedGroup = _credentialsPresenter.Groups.FirstOrDefault(group =>
                string.Equals(group.GroupId, _credentialsPresenter.SelectedManagedGroupId, StringComparison.Ordinal));

            _credentialWorkspace.ManagedGroupNameTextBox.Text = selectedGroup?.GroupName ?? string.Empty;
            _credentialWorkspace.ManagedGroupNameTextBox.Enabled = selectedGroup != null && !_credentialsPresenter.IsLoading;
            _credentialWorkspace.GroupSaveButton.Enabled = !_credentialsPresenter.IsLoading && (_credentialsPresenter.WorkspaceMode == CredentialWorkspaceMode.CreateGroup || selectedGroup != null);
            _credentialWorkspace.GroupDeleteButton.Enabled = !_credentialsPresenter.IsLoading && _credentialsPresenter.WorkspaceMode == CredentialWorkspaceMode.ManageGroup && selectedGroup != null;
            _credentialWorkspace.GroupCreateButton.Enabled = !_credentialsPresenter.IsLoading;

            if (_credentialsPresenter.WorkspaceMode == CredentialWorkspaceMode.CreateGroup)
            {
                _credentialWorkspace.NewGroupNameTextBox.Enabled = !_credentialsPresenter.IsLoading;
            }
            else
            {
                _credentialWorkspace.NewGroupNameTextBox.Text = string.Empty;
                _credentialWorkspace.NewGroupNameTextBox.Enabled = false;
            }

            if (string.IsNullOrWhiteSpace(_credentialsPresenter.GroupStatusMessage))
            {
                if (_credentialsPresenter.Groups.Count == 0 && _credentialsPresenter.WorkspaceMode != CredentialWorkspaceMode.CreateGroup) {
                    _credentialWorkspace.GroupStatusLabel.Text = "No groups returned for the active server.";
                }
            }
        }

        private BrowserRegistryDto BuildCredentialRegistryFromEditor()
        {
            return new BrowserRegistryDto
            {
                Domain = _credentialWorkspace.DomainTextBox.Text.Trim(),
                Username = _credentialWorkspace.UsernameTextBox.Text.Trim(),
                Password = _credentialWorkspace.PasswordTextBox.Text,
                Description = _credentialWorkspace.DescriptionTextBox.Text.Trim()
            };
        }

        private string GetSelectedAssignedCredentialGroupId()
        {
            return _credentialWorkspace.AssignedGroupComboBox.SelectedItem is SelectionOption option
                ? option.Value
                : string.Empty;
        }

        private void LayoutCredentialEntryEditor()
        {
            _credentialWorkspace.LayoutEditor(_quickPanelDetailPanel.Height);
        }
    }
}
