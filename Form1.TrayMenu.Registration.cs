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
            _trayMenuController?.SetActiveSection(TrayMenuSection.Servers);
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
                    ? "Generating QR code for new server registration."
                    : "Generating QR code for namespace sync.";
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
                _serverRegistrationStatusMessage = "QR generated. Waiting for mobile confirmation.";
                _lastTrayMessage = isNewServerRegistration
                    ? "New server registration QR generated. Waiting for mobile confirmation."
                    : "Namespace sync QR generated. Waiting for mobile confirmation.";
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