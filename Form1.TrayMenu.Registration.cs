using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrayApp.Models;
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

        private void quickPanelServerRegistrationRestartButton_Click(object? sender, EventArgs e)
        {
            _bootstrapRegistrationService.CancelRegistrationPolling();
            ResetInlineServerRegistrationState(clearInputs: true);
            UpdateQuickPanelState();
        }

        private void restartRegistrationButton_Click(object? sender, EventArgs e)
        {
            _bootstrapRegistrationService.CancelRegistrationPolling();
            pictureBoxQr.Image = null;

            if (_registrationPromptMode == RegistrationPromptMode.ServerRegistration)
            {
                ShowNamespaceSyncPrompt();
            }
            else if (_registrationPromptMode == RegistrationPromptMode.NewServerRegistration)
            {
                ShowServerRegistrationPrompt();
            }
            else
            {
                ShowBaseUrlPrompt();
            }
        }

        private void quickPanelRegisterUserInlineButton_Click(object? sender, EventArgs e)
        {
            if (_authenticationInProgress || _authenticationService.IsAuthorizedForActiveNamespace)
            {
                return;
            }

            ShowBaseUrlPrompt();
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
            _inlineUserRegistrationSubmitting = false;
            _inlineUserRegistrationAwaitingCompletion = false;
            _quickPanelUserRegistrationStatusLabel.Text = string.Empty;
            _quickPanelUserRegistrationQrPictureBox.Image = null;
            _quickPanelUserRegistrationQrPictureBox.Visible = false;
            UpdateUserRegistrationBackButton();
            _quickPanelUserRegistrationSubmitButton.Text = "Continue";
            _quickPanelUserRegistrationSubmitButton.Enabled = true;

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
            UpdateInlineUserDeletionAvailability();
            UpdateQuickPanelState();
        }

        private void quickPanelUserDeletionCancelButton_Click(object? sender, EventArgs e)
        {
            _showInlineUserDeletion = false;
            UpdateQuickPanelState();
        }

        private async void quickPanelUserDeletionConfirmButton_Click(object? sender, EventArgs e)
        {
            if (!CanDeleteActiveProfile())
            {
                UpdateInlineUserDeletionAvailability();
                return;
            }

            _quickPanelUserDeletionConfirmButton.Enabled = false;
            _quickPanelUserDeletionConfirmButton.Text = "Deleting...";
            await DeleteActiveUserProfileAsync();
        }

        private void UpdateInlineUserDeletionAvailability()
        {
            bool canDeleteProfile = CanDeleteActiveProfile();
            _quickPanelUserDeletionConfirmButton.Enabled = canDeleteProfile;
            _quickPanelUserDeletionConfirmButton.Text = canDeleteProfile ? "Delete profile" : "Login required";
        }

        private bool CanDeleteActiveProfile()
        {
            if (!_registeredDeviceService.TryReadRegistration(out DesktopRegistration? registration) ||
                registration == null ||
                registration.Namespaces.Count == 0)
            {
                return false;
            }

            return registration.Namespaces.All(namespaceItem =>
                _authorizationSessionStore.HasActiveSessionForNamespace(registration.UserKey, namespaceItem.NamespaceId));
        }

        internal static async Task DeleteProfileRegistrationsAsync(
            string userId,
            RegistrationConfigStore configStore,
            AuthorizationSessionStore sessions,
            Func<NamespaceItemDto, Task> deleteDevice)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("Profile id must not be empty.", nameof(userId));
            }

            DesktopRegistration profile = configStore.ReadRegistration(userId);
            if (!string.Equals(profile.UserKey, userId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The selected profile could not be found.");
            }

            void EnsureAuthorized(NamespaceItemDto server)
            {
                if (string.IsNullOrWhiteSpace(server.NamespaceId) ||
                    string.IsNullOrWhiteSpace(server.Url) ||
                    string.IsNullOrWhiteSpace(server.AccountId) ||
                    string.IsNullOrWhiteSpace(server.DesktopDeviceId))
                {
                    throw new InvalidDataException(
                        $"Server '{server.Url}' does not contain valid profile registration credentials.");
                }

                if (!sessions.GetStates(userId).Any(session =>
                    string.Equals(session.NamespaceId, server.NamespaceId, StringComparison.Ordinal) &&
                    string.Equals(session.BaseUrl, server.Url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(session.AccountId, server.AccountId, StringComparison.Ordinal) &&
                    string.Equals(session.DesktopId, server.DesktopDeviceId, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        $"Log in with this profile to server '{server.Url}' before deleting the profile. " +
                        "Profile deletion requires an active session on every registered server.");
                }
            }

            foreach (NamespaceItemDto server in profile.Namespaces)
            {
                EnsureAuthorized(server);
            }

            // A single device registration can cover multiple namespaces on the same server.
            var registrations = profile.Namespaces.GroupBy(server => (
                server.Url.TrimEnd('/').ToUpperInvariant(), server.AccountId, server.DesktopDeviceId));
            foreach (var registration in registrations)
            {
                NamespaceItemDto server = registration.First();
                try
                {
                    foreach (NamespaceItemDto namespaceItem in registration)
                    {
                        EnsureAuthorized(namespaceItem);
                    }

                    await deleteDevice(server);
                    foreach (NamespaceItemDto namespaceItem in registration)
                    {
                        configStore.RemoveNamespace(userId, namespaceItem.NamespaceId);
                        sessions.Revoke(userId, namespaceItem.NamespaceId);
                    }
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Profile deletion stopped on server '{server.Url}'. " +
                        $"Remaining registrations are kept for retry. {exception.Message}", exception);
                }
            }
        }

        private void ResetInlineUserRegistration(bool keepFormOpen = false)
        {
            _showInlineUserRegistration = keepFormOpen;
            _inlineUserRegistrationSubmitting = false;
            _inlineUserRegistrationAwaitingCompletion = false;
            _quickPanelUserRegistrationQrPictureBox.Image = null;
            _quickPanelUserRegistrationQrPictureBox.Visible = false;
            UpdateUserRegistrationBackButton();
            _quickPanelUserRegistrationStatusLabel.Text = string.Empty;
            _quickPanelUserRegistrationSubmitButton.Text = "Continue";
            _quickPanelUserRegistrationSubmitButton.Enabled = true;
        }

        private void UpdateUserRegistrationBackButton()
        {
            bool hasQrCode = _quickPanelUserRegistrationQrPictureBox.Image != null;
            _quickPanelUserRegistrationUrlTextBox.ReadOnly = hasQrCode;
            _quickPanelUserRegistrationNameTextBox.ReadOnly = hasQrCode;
            _quickPanelUserRegistrationBackButton.Text = hasQrCode ? "Cancel" : "Back";
            _quickPanelUserRegistrationBackButton.Width = 90;
            _quickPanelUserRegistrationSubmitButton.Left = _quickPanelUserRegistrationBackButton.Right + 5;
        }

        private void quickPanelUserRegistrationBackButton_Click(object? sender, EventArgs e)
        {
            bool hasQrCode = _quickPanelUserRegistrationQrPictureBox.Image != null;
            _bootstrapRegistrationService.CancelRegistrationPolling();
            ResetInlineUserRegistration(keepFormOpen: hasQrCode);
            UpdateQuickPanelState();
            if (hasQrCode)
            {
                _quickPanelUserRegistrationNameTextBox.Focus();
            }
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

                Bitmap? qrCode = _bootstrapRegistrationService.QrCode;
                if (qrCode == null)
                {
                    throw new InvalidOperationException("QR code is empty.");
                }

                _quickPanelUserRegistrationQrPictureBox.Image = qrCode;
                _quickPanelUserRegistrationQrPictureBox.Visible = true;
                UpdateUserRegistrationBackButton();
                _inlineUserRegistrationAwaitingCompletion = true;
                _quickPanelUserRegistrationSubmitButton.Text = "QR ready";
                _quickPanelUserRegistrationStatusLabel.Text = "QR generated. Waiting for mobile confirmation.";
                _lastTrayMessage = "User registration QR generated. Waiting for mobile confirmation.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                _bootstrapRegistrationService.StartRegistrationPolling();
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

                Bitmap? qrCode = _bootstrapRegistrationService.QrCode;
                if (qrCode == null)
                {
                    throw new InvalidOperationException("QR code is empty.");
                }

                _quickPanelServerRegistrationQrPictureBox.Image = qrCode;
                _quickPanelServerRegistrationQrPictureBox.Visible = true;
                _quickPanelServerRegistrationRestartButton.Visible = true;
                _inlineServerRegistrationAwaitingCompletion = true;
                _serverRegistrationStatusMessage = "QR generated. Scan it in the mobile app to complete registration.";
                _lastTrayMessage = isNewServerRegistration
                    ? "Server QR generated. Scan it in the mobile app to complete registration."
                    : "Server sync QR generated. Scan it in the mobile app to complete registration.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                _bootstrapRegistrationService.StartRegistrationPolling(_quickPanelServerRegistrationUrlTextBox.Text);
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