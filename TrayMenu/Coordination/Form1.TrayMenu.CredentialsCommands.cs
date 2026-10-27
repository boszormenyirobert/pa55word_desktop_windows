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
        private void InitializeCredentialWorkspacePanel()
        {
            _credentialSelection.GroupChanged += HandleCredentialGroupChanged;
            _credentialSelection.EntryChanged += HandleCredentialEntryChanged;
            _credentialSelection.ManageGroupsRequested += quickPanelCredentialManageGroupsButton_Click;
            _credentialSelection.CreateRequested += quickPanelCredentialCreateButton_Click;
            _credentialWorkspace.BackRequested += quickPanelCredentialBackButton_Click;
            _credentialWorkspace.UsernameCopyRequested += quickPanelCredentialUsernameCopyButton_Click;
            _credentialWorkspace.PasswordGenerateRequested += quickPanelCredentialPasswordGenerateButton_Click;
            _credentialWorkspace.PasswordRevealRequested += quickPanelCredentialPasswordRevealButton_Click;
            _credentialWorkspace.PasswordCopyRequested += quickPanelCredentialPasswordCopyButton_Click;
            _credentialWorkspace.DescriptionCopyRequested += quickPanelCredentialDescriptionCopyButton_Click;
            _credentialWorkspace.DeleteRequested += HandleCredentialDeleteRequested;
            _credentialWorkspace.SaveRequested += HandleCredentialSaveRequested;
            _credentialWorkspace.ManagedGroupChanged += quickPanelManagedGroupComboBox_SelectedIndexChanged;
            _credentialWorkspace.GroupDeleteRequested += HandleCredentialGroupDeleteRequested;
            _credentialWorkspace.GroupSaveRequested += HandleCredentialGroupSaveRequested;
            _credentialWorkspace.GroupCreateRequested += quickPanelCredentialGroupCreateButton_Click;
            _credentialWorkspace.EntryInputChanged += credentialEditorInput_TextChanged;
            _credentialWorkspace.EntrySelectionChanged += credentialEditorInput_Changed;
            _credentialWorkspace.GroupInputChanged += groupEditorInput_TextChanged;
        }

        private void HandleCredentialGroupChanged(object? sender, EventArgs e)
        {
            if (_updatingQuickPanelSelections ||
                _credentialSelection.GroupSelector.SelectedItem is not SelectionOption option)
                return;
            _credentialsPresenter.SelectGroup(option.Value);
            PopulateCredentialSelectors();
            RefreshCredentialQuickPanelState();
        }

        private void HandleCredentialEntryChanged(object? sender, EventArgs e)
        {
            if (_updatingQuickPanelSelections ||
                _credentialSelection.EntrySelector.SelectedItem is not SelectionOption option)
                return;
            _credentialsPresenter.SelectedEntryId = option.Value;
            _credentialsPresenter.WorkspaceMode = CredentialWorkspaceMode.ViewEntry;
            RefreshCredentialQuickPanelState();
        }

        private bool IsDeletionConfirmationPending(CredentialDeletionConfirmation confirmation)
        {
            return _credentialsPresenter.DeletionConfirmation == confirmation;
        }

        private void ClearCredentialEditorStatus()
        {
            _credentialsPresenter.EntryStatusMessage = string.Empty;
            _credentialWorkspace.StatusLabel.Text = string.Empty;
        }

        private void SetCredentialPasswordVisible(bool visible)
        {
            _credentialWorkspace.PasswordTextBox.UseSystemPasswordChar = !visible;
        }

        private void SetCredentialEditorStatus(string message)
        {
            _credentialsPresenter.EntryStatusMessage = message;
            _credentialWorkspace.StatusLabel.Text = message;
        }

        private static string GenerateCredentialPassword(int length = 18)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*()-_=+";
            return new string(
                Enumerable.Range(0, length)
                    .Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)])
                    .ToArray());
        }

        private void CopyCredentialValueToClipboard(string value, string fieldName)
        {
            if (string.IsNullOrEmpty(value))
            {
                SetCredentialEditorStatus($"{fieldName} is empty.");
                return;
            }

            try
            {
                Clipboard.SetText(value);
                SetCredentialEditorStatus($"{fieldName} copied.");
            }
            catch (Exception ex)
            {
                SetCredentialEditorStatus(ex.Message);
            }
        }

        private void ClearGroupEditorStatus()
        {
            _credentialsPresenter.GroupStatusMessage = string.Empty;
            _credentialWorkspace.GroupStatusLabel.Text = string.Empty;
        }

        private void credentialEditorInput_TextChanged(object? sender, EventArgs e)
        {
            if (_updatingCredentialWorkspaceControls)
            {
                return;
            }

            ClearCredentialEditorStatus();
        }

        private void credentialEditorInput_Changed(object? sender, EventArgs e)
        {
            if (_updatingCredentialWorkspaceControls)
            {
                return;
            }

            ClearCredentialEditorStatus();
        }

        private void quickPanelCredentialPasswordGenerateButton_Click(object? sender, EventArgs e)
        {
            _credentialWorkspace.PasswordTextBox.Text = GenerateCredentialPassword();
            SetCredentialPasswordVisible(false);
            SetCredentialEditorStatus("Password generated.");
        }

        private void quickPanelCredentialPasswordRevealButton_Click(object? sender, EventArgs e)
        {
            SetCredentialPasswordVisible(_credentialWorkspace.PasswordTextBox.UseSystemPasswordChar);
        }

        private void quickPanelCredentialPasswordCopyButton_Click(object? sender, EventArgs e)
        {
            CopyCredentialValueToClipboard(_credentialWorkspace.PasswordTextBox.Text, "Password");
        }

        private void quickPanelCredentialUsernameCopyButton_Click(object? sender, EventArgs e)
        {
            CopyCredentialValueToClipboard(_credentialWorkspace.UsernameTextBox.Text, "Username");
        }

        private void quickPanelCredentialDescriptionCopyButton_Click(object? sender, EventArgs e)
        {
            CopyCredentialValueToClipboard(_credentialWorkspace.DescriptionTextBox.Text, "Description");
        }

        private void groupEditorInput_TextChanged(object? sender, EventArgs e)
        {
            if (_updatingCredentialWorkspaceControls)
            {
                return;
            }

            ClearGroupEditorStatus();
        }

        private void quickPanelCredentialCreateButton_Click(object? sender, EventArgs e)
        {
            if (_credentialsPresenter.WorkspaceMode is CredentialWorkspaceMode.ManageGroup or CredentialWorkspaceMode.CreateGroup)
            {
                EnterCredentialGroupManager(showCreateGroup: true);
                return;
            }

            EnterCredentialCreateMode();
        }

        private void quickPanelCredentialManageGroupsButton_Click(object? sender, EventArgs e)
        {
            EnterCredentialGroupManager(showCreateGroup: false);
        }

        private void quickPanelCredentialBackButton_Click(object? sender, EventArgs e)
        {
            ClearCredentialEditorStatus();
            ClearGroupEditorStatus();
            SetCredentialWorkspaceMode(_credentialsPresenter.WorkspaceMode == CredentialWorkspaceMode.CreateGroup
                ? CredentialWorkspaceMode.ManageGroup
                : CredentialWorkspaceMode.ViewEntry);
        }

        private void quickPanelCredentialGroupCreateButton_Click(object? sender, EventArgs e)
        {
            EnterCredentialGroupManager(showCreateGroup: true);
        }

        private void EnterCredentialCreateMode()
        {
            ClearCredentialEditorStatus();
            _credentialsPresenter.WorkspaceMode = CredentialWorkspaceMode.CreateEntry;
            ResetCredentialCreateEditor();
            RenderCredentialWorkspace();
        }

        private void EnterCredentialGroupManager(bool showCreateGroup)
        {
            if (!showCreateGroup && !string.IsNullOrWhiteSpace(_credentialsPresenter.SelectedGroupId))
            {
                _credentialsPresenter.SelectedManagedGroupId = _credentialsPresenter.SelectedGroupId;
            }

            if (showCreateGroup)
            {
                _credentialWorkspace.NewGroupNameTextBox.Text = string.Empty;
            }

            ClearGroupEditorStatus();
            SetCredentialWorkspaceMode(showCreateGroup
                ? CredentialWorkspaceMode.CreateGroup
                : CredentialWorkspaceMode.ManageGroup);
        }

        private void SetCredentialWorkspaceMode(CredentialWorkspaceMode mode)
        {
            _credentialsPresenter.WorkspaceMode = mode;
            RenderCredentialWorkspace();
        }

        private void quickPanelManagedGroupComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_updatingCredentialWorkspaceControls || _credentialWorkspace.ManagedGroupComboBox.SelectedItem is not SelectionOption option)
            {
                return;
            }

            _credentialsPresenter.SelectedManagedGroupId = option.Value;
            ClearGroupEditorStatus();
            RenderCredentialWorkspace();
        }
    }
}
