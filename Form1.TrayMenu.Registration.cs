using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrayApp.Services;
using TrayApp.TrayMenu;

namespace TrayApp
{
    public partial class Form1
    {
        private void quickPanelRegisterNewServerInlineButton_Click(object? sender, EventArgs e)
        {
            ShowInlineServerRegistration(InlineServerRegistrationMode.NewServer);
        }

        private void quickPanelServerRegistrationInlineButton_Click(object? sender, EventArgs e)
        {
            ShowInlineServerRegistration(InlineServerRegistrationMode.NamespaceSync);
        }

        private void ShowInlineServerRegistration(InlineServerRegistrationMode registrationMode)
        {
            if (_inlineServerRegistrationAwaitingCompletion || _inlineServerRegistrationSubmitting)
            {
                return;
            }

            if (_showInlineServerRegistration)
            {
                ResetInlineServerRegistrationState(clearInputs: false);
                UpdateQuickPanelState();
                return;
            }

            _showInlineServerRegistration = true;
            _inlineServerRegistrationMode = registrationMode;
            _serverRegistrationStatusMessage = string.Empty;
            _quickPanelServerRegistrationQrPictureBox.Image = null;

            try
            {
                _quickPanelServerRegistrationUrlTextBox.Text = AppEnvironment.BackendBaseUrl;
            }
            catch
            {
                _quickPanelServerRegistrationUrlTextBox.Text = string.Empty;
            }

            _quickPanelServerRegistrationNameTextBox.Text = string.Empty;
            UpdateQuickPanelState();
            _quickPanelServerRegistrationUrlTextBox.Focus();
        }

        private void quickPanelServerRegistrationBackButton_Click(object? sender, EventArgs e)
        {
            _bootstrapRegistrationService.CancelRegistrationPolling();
            ResetInlineServerRegistrationState(clearInputs: true);
            _trayMenuController?.SetActiveSection(TrayMenuSection.SettingsServers);
            UpdateQuickPanelState();
        }

        private void quickPanelRegisterUserInlineButton_Click(object? sender, EventArgs e)
        {
            if (_authenticationInProgress || _authenticationService.IsAuthorizedForActiveNamespace)
            {
                return;
            }

            ShowBaseUrlPrompt(showAsTrayPrompt: true);
        }

        private void ShowInlineUserRegistration()
        {
            if (_authenticationInProgress ||
                _authenticationService.HasAnyRegisteredProfileWithActiveSession ||
                _inlineUserRegistrationSubmitting ||
                _inlineUserRegistrationAwaitingCompletion)
            {
                return;
            }

            _showInlineUserDeletion = false;
            _showInlineUserRegistration = true;
            _quickPanelUserRegistrationStatusLabel.Text = string.Empty;
            _quickPanelUserRegistrationQrPictureBox.Image = null;
            _quickPanelUserRegistrationQrPictureBox.Visible = false;
            _quickPanelUserRegistrationSubmitButton.Text = "Continue";

            try
            {
                _quickPanelUserRegistrationUrlTextBox.Text = AppEnvironment.BackendBaseUrl;
            }
            catch
            {
                _quickPanelUserRegistrationUrlTextBox.Text = _bootstrapRegistrationService.GetBootstrapBaseUrl();
            }

            _quickPanelUserRegistrationNameTextBox.Text = string.Empty;
            UpdateQuickPanelState();
            _quickPanelUserRegistrationNameTextBox.Focus();
        }

        private void ShowInlineUserDeletion()
        {
            if (_authenticationInProgress || !_authenticationService.IsAuthorized)
            {
                return;
            }

            _showInlineUserRegistration = false;
            _showInlineUserDeletion = true;
            string profileName = _registeredDeviceService.TryReadRegistration(out var registration) && registration != null
                ? registration.DisplayUserName
                : AppEnvironment.GetActiveUserId() ?? "Unknown profile";
            _quickPanelUserDeletionProfileLabel.Text = $"Selected active profile to be deleted: {profileName}";
            _quickPanelUserDeletionConfirmButton.Enabled = true;
            _quickPanelUserDeletionConfirmButton.Text = "Delete profile";
            UpdateQuickPanelState();
        }

        private void quickPanelUserDeletionCancelButton_Click(object? sender, EventArgs e)
        {
            _showInlineUserDeletion = false;
            UpdateQuickPanelState();
        }

        private async void quickPanelUserDeletionConfirmButton_Click(object? sender, EventArgs e)
        {
            _quickPanelUserDeletionConfirmButton.Enabled = false;
            _quickPanelUserDeletionConfirmButton.Text = "Deleting...";
            await DeleteActiveUserProfileAsync();
        }

        private void ResetInlineUserRegistration()
        {
            _showInlineUserRegistration = false;
            _inlineUserRegistrationSubmitting = false;
            _inlineUserRegistrationAwaitingCompletion = false;
            _quickPanelUserRegistrationQrPictureBox.Image = null;
            _quickPanelUserRegistrationQrPictureBox.Visible = false;
            _quickPanelUserRegistrationStatusLabel.Text = string.Empty;
            _quickPanelUserRegistrationSubmitButton.Text = "Continue";
        }

        private void quickPanelUserRegistrationBackButton_Click(object? sender, EventArgs e)
        {
            _bootstrapRegistrationService.CancelRegistrationPolling();
            ResetInlineUserRegistration();
            UpdateQuickPanelState();
        }

        private async void quickPanelUserRegistrationSubmitButton_Click(object? sender, EventArgs e)
        {
            if (_inlineUserRegistrationSubmitting || _inlineUserRegistrationAwaitingCompletion)
            {
                return;
            }

            try
            {
                _inlineUserRegistrationSubmitting = true;
                _quickPanelUserRegistrationSubmitButton.Enabled = false;
                _quickPanelUserRegistrationStatusLabel.Text = "Generating QR code...";

                await _bootstrapRegistrationService
                    .PrepareRegistrationQrAsync(
                        _quickPanelUserRegistrationUrlTextBox.Text,
                        _quickPanelUserRegistrationNameTextBox.Text)
                    .ConfigureAwait(true);

                Bitmap? qrCode = AppServices.Qr.GetQrCode();
                if (qrCode == null)
                {
                    throw new InvalidOperationException("QR code is empty.");
                }

                _quickPanelUserRegistrationQrPictureBox.Image = qrCode;
                _quickPanelUserRegistrationQrPictureBox.Visible = true;
                _inlineUserRegistrationAwaitingCompletion = true;
                _quickPanelUserRegistrationSubmitButton.Text = "QR ready";
                _quickPanelUserRegistrationStatusLabel.Text = "QR generated. Waiting for mobile confirmation.";
                _lastTrayMessage = "User registration QR generated. Waiting for mobile confirmation.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                _ = StartPollingAfterRenderAsync();
            }
            catch (Exception exception)
            {
                _quickPanelUserRegistrationStatusLabel.Text = exception.Message;
            }
            finally
            {
                _inlineUserRegistrationSubmitting = false;
                _quickPanelUserRegistrationSubmitButton.Enabled = !_inlineUserRegistrationAwaitingCompletion;
            }
        }

        private async void quickPanelServerRegistrationSubmitButton_Click(object? sender, EventArgs e)
        {
            if (_inlineServerRegistrationSubmitting || _inlineServerRegistrationAwaitingCompletion)
            {
                return;
            }

            try
            {
                bool isNewServerRegistration = _inlineServerRegistrationMode == InlineServerRegistrationMode.NewServer;
                _registrationPromptMode = isNewServerRegistration
                    ? RegistrationPromptMode.NewServerRegistration
                    : RegistrationPromptMode.ServerRegistration;
                _inlineServerRegistrationSubmitting = true;
                _serverRegistrationStatusMessage = isNewServerRegistration
                    ? "Generating QR code to add the server."
                    : "Generating QR code to sync the server.";
                UpdateQuickPanelState();

                if (isNewServerRegistration)
                {
                    await _bootstrapRegistrationService
                        .PrepareNewServerRegistrationQrAsync(
                            _quickPanelServerRegistrationUrlTextBox.Text,
                            _quickPanelServerRegistrationNameTextBox.Text)
                        .ConfigureAwait(true);
                }
                else
                {
                    await _bootstrapRegistrationService
                        .PrepareServerRegistrationQrAsync(_quickPanelServerRegistrationUrlTextBox.Text)
                        .ConfigureAwait(true);
                }

                Bitmap? qrCode = AppServices.Qr.GetQrCode();
                if (qrCode == null)
                {
                    throw new InvalidOperationException("QR code is empty.");
                }

                _quickPanelServerRegistrationQrPictureBox.Image = qrCode;
                _inlineServerRegistrationAwaitingCompletion = true;
                _serverRegistrationStatusMessage = "QR generated. Scan it in the mobile app to complete registration.";
                _lastTrayMessage = isNewServerRegistration
                    ? "Server QR generated. Scan it in the mobile app to complete registration."
                    : "Server sync QR generated. Scan it in the mobile app to complete registration.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                _ = StartPollingAfterRenderAsync(_quickPanelServerRegistrationUrlTextBox.Text);
            }
            catch (Exception ex)
            {
                _serverRegistrationStatusMessage = ex.Message;
            }
            finally
            {
                _inlineServerRegistrationSubmitting = false;
                UpdateQuickPanelState();
            }
        }
    }
}