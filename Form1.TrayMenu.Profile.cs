using System.Drawing;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;

namespace TrayApp
{
    public partial class Form1
    {
        private void InitializeAccountRenameControls()
        {
            _quickPanelAccountRenamePanel.BackColor = Color.FromArgb(41, 43, 71);
            _quickPanelAccountRenamePanel.Location = new Point(32, 295);
            _quickPanelAccountRenamePanel.Size = new Size(578, 140);
            _quickPanelAccountRenamePanel.Visible = false;
            var nameLabel = new Label();
            ConfigureCredentialWorkspaceLabel(nameLabel, "Profile name", new Point(16, 16));
            ConfigureCredentialWorkspaceTextBox(
                _quickPanelAccountNameTextBox, new Point(16, 40), new Size(544, 25), multiline: false);
            ConfigureCredentialWorkspaceButton(
                _quickPanelAccountRenameSubmitButton, "Save name", new Point(16, 90), RenameAccount_Click);
            _quickPanelAccountRenameSubmitButton.Size = new Size(120, 30);
            _quickPanelAccountRenamePanel.Controls.Add(nameLabel);
            _quickPanelAccountRenamePanel.Controls.Add(_quickPanelAccountNameTextBox);
            _quickPanelAccountRenamePanel.Controls.Add(_quickPanelAccountRenameSubmitButton);
            _quickPanelDetailPanel.Controls.Add(_quickPanelAccountRenamePanel);
        }

        private void ShowAccountRename()
        {
            try
            {
                if (!_authenticationService.IsAuthorized || _authenticationInProgress)
                {
                    throw new InvalidOperationException("Log in to at least one server to manage your account name.");
                }

                DesktopRegistration registration = _registeredDeviceService.ReadRegistration();
                _accountRenameUserId = registration.UserKey;
                _quickPanelAccountNameTextBox.Text = registration.DisplayUserName;
                _showInlineUserRegistration = false;
                _showInlineUserDeletion = false;
                _showInlineAccountRename = true;

                UpdateQuickPanelState();
                _quickPanelAccountNameTextBox.Focus();
                _quickPanelAccountNameTextBox.SelectAll();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Account settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RenameAccount_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_authenticationInProgress || _accountRenameUserId == null)
                {
                    throw new InvalidOperationException("Select the signed-in account before renaming it.");
                }

                _authenticationService.RenameProfile(_accountRenameUserId, _quickPanelAccountNameTextBox.Text);
                _showInlineAccountRename = false;
                _accountRenameUserId = null;
                RefreshQuickPanelSelections();
                UpdateQuickPanelState();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Rename profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateAccountRenamePresentation()
        {
            if (!string.Equals(_accountRenameUserId, AppEnvironment.GetActiveUserId(), StringComparison.Ordinal))
            {
                _showInlineAccountRename = false;
                _accountRenameUserId = null;
            }

            _quickPanelAccountRenamePanel.Visible =
                _trayMenuController?.ActiveSection == TrayMenuSection.SettingsUsers && _showInlineAccountRename;
            _quickPanelAccountRenameSubmitButton.Enabled =
                _authenticationService.IsAuthorized && !_authenticationInProgress;
        }
    }
}
