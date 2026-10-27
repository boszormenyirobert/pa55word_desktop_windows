using TrayApp.TrayMenu.Controls;
using System.Drawing;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;

namespace TrayApp
{
    public partial class Form1
    {
        private readonly ProfileControl _profileView = new();
        private bool _showInlineAccountRename;
        private string? _accountRenameUserId;
        private bool _showInlineUserRegistration;
        private bool _showInlineUserDeletion;
        private bool _inlineUserRegistrationSubmitting;
        private bool _inlineUserRegistrationAwaitingCompletion;

        private void InitializeAccountRenameControls()
        {
            _profileView.ActionChanged += quickPanelSettingsUserActionComboBox_SelectedIndexChanged;
            _profileView.RenameRequested += RenameAccount_Click;
            _profileView.RegistrationBackRequested += quickPanelUserRegistrationBackButton_Click;
            _profileView.RegistrationSubmitRequested += quickPanelUserRegistrationSubmitButton_Click;
            _profileView.DeletionCancelRequested += quickPanelUserDeletionCancelButton_Click;
            _profileView.DeletionConfirmRequested += quickPanelUserDeletionConfirmButton_Click;
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
                _profileView.ProfileName = registration.DisplayUserName;
                _showInlineUserRegistration = false;
                _showInlineUserDeletion = false;
                _showInlineAccountRename = true;

                UpdateQuickPanelState();
                _profileView.FocusProfileName();
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

                _authenticationService.RenameProfile(_accountRenameUserId, _profileView.ProfileName);
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

            _profileView.ShowSection(
                _trayMenuController?.ActiveSection == TrayMenuSection.SettingsUsers,
                _showInlineAccountRename, _showInlineUserRegistration, _showInlineUserDeletion,
                _authenticationService.IsAuthorized && !_authenticationInProgress);
        }

        private void PopulateSettingsUserActions()
        {
            bool canCreateProfile = !_authenticationInProgress &&
                !_authenticationService.HasAnyRegisteredProfileWithActiveSession;
            bool canDeleteProfile = _authenticationService.IsAuthorized;

            _profileView.ActionComboBox.Items.Clear();
            _profileView.ActionComboBox.Items.Add(new SelectionOption("Select profile action", string.Empty));
            if (canCreateProfile)
            {
                _profileView.ActionComboBox.Items.Add(new SelectionOption("Create new profile", "create"));
            }

            if (canDeleteProfile)
            {
                _profileView.ActionComboBox.Items.Add(new SelectionOption("Rename profile", "rename"));
                _profileView.ActionComboBox.Items.Add(new SelectionOption("Delete profile", "delete"));
            }
            _profileView.ActionComboBox.SetSelectedIndexSilently(0);
        }

        private void quickPanelSettingsUserActionComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_profileView.ActionComboBox.SelectedItem is not SelectionOption option ||
                _trayMenuController?.ActiveSection != TrayMenuSection.SettingsUsers)
            {
                return;
            }

            _profileView.ActionComboBox.SetSelectedIndexSilently(0);

            _showInlineAccountRename = false;
            if (option.Value == "rename")
            {
                ShowAccountRename();
                return;
            }

            if (option.Value == "create")
            {
                ShowInlineUserRegistration();
                return;
            }

            if (option.Value == "delete")
            {
                ShowInlineUserDeletion();
            }
        }

        private async Task DeleteActiveUserProfileAsync()
        {
            if (_authenticationInProgress || !_authenticationService.IsAuthorized)
            {
                return;
            }

            string? activeUserId = AppEnvironment.GetActiveUserId();
            if (string.IsNullOrWhiteSpace(activeUserId))
            {
                return;
            }

            var configStore = new RegistrationConfigStore();
            string? originalNamespaceId = AppEnvironment.GetActiveNamespaceId();
            try
            {
                _authenticationInProgress = true;
                _profileView.DeletionConfirmButton.Enabled = false;
                _profileView.DeletionConfirmButton.Text = "Deleting...";
                _profileView.DeletionCancelButton.Enabled = false;
                UpdateAuthenticationMenuItems();

                await DeleteProfileRegistrationsAsync(
                    activeUserId,
                    configStore,
                    _authorizationSessionStore,
                    namespaceItem => _accountDeviceService.DeleteDeviceAsync(
                        new AccountDeviceDto { DesktopDeviceId = namespaceItem.DesktopDeviceId },
                        namespaceItem.Url,
                        namespaceItem.AccountId,
                        namespaceItem.DesktopDeviceId,
                        removeLocalRegistrationOnDeviceNotFound: false)).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Delete user profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _authenticationInProgress = false;
                _profileView.DeletionConfirmButton.Enabled = true;
                _profileView.DeletionConfirmButton.Text = "Delete profile";
                _profileView.DeletionCancelButton.Enabled = true;

                if (configStore.TryReadRegistrations(out List<DesktopRegistration>? registrations) &&
                    registrations is { Count: > 0 })
                {
                    DesktopRegistration? remainingRegistration = registrations.FirstOrDefault(registration =>
                        string.Equals(registration.UserKey, activeUserId, StringComparison.Ordinal));
                    AppEnvironment.SetActiveUser(remainingRegistration?.UserKey ?? registrations[0].UserKey);
                    _isDeviceRegistered = true;

                    if (remainingRegistration == null)
                    {
                        _showInlineUserDeletion = false;
                        _extensionSessionStateStore.MarkInvalid("authorization_revoked");
                        _trayMenuController?.SetActiveSection(TrayMenuSection.Login, triggerStateChanged: true);
                    }
                }
                else
                {
                    _showInlineUserDeletion = false;
                    AppEnvironment.ClearActiveSelection();
                    _isDeviceRegistered = false;
                    _extensionSessionStateStore.MarkInvalid("user_profile_deleted");
                    ShowBaseUrlPrompt();
                }

                if (!string.Equals(activeUserId, AppEnvironment.GetActiveUserId(), StringComparison.Ordinal) ||
                    !string.Equals(originalNamespaceId, AppEnvironment.GetActiveNamespaceId(), StringComparison.Ordinal))
                {
                    _credentialsPresenter.InvalidateCollection();
                    _credentialsPresenter.SelectedGroupId = string.Empty;
                    _credentialsPresenter.SelectedEntryId = string.Empty;
                    _credentialSelectorsFingerprint = string.Empty;
                    ResetDevicesState();
                    if (_isDeviceRegistered)
                    {
                        _extensionSessionStateStore.MarkInvalid("authorization_revoked");
                    }
                }

                RefreshQuickPanelSelections();
                UpdateQuickPanelState();
                UpdateAuthenticationMenuItems();
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
            _profileView.RegistrationStatusLabel.Text = string.Empty;
            _profileView.RegistrationQrPictureBox.Image = null;
            _profileView.RegistrationQrPictureBox.Visible = false;
            UpdateUserRegistrationBackButton();
            _profileView.RegistrationSubmitButton.Text = "Continue";
            _profileView.RegistrationSubmitButton.Enabled = true;

            try
            {
                _profileView.RegistrationUrlTextBox.Text = AppEnvironment.BackendBaseUrl;
            }
            catch
            {
                _profileView.RegistrationUrlTextBox.Text = _bootstrapRegistrationService.GetBootstrapBaseUrl();
            }

            _profileView.RegistrationNameTextBox.Text = string.Empty;
            UpdateQuickPanelState();
            _profileView.RegistrationNameTextBox.Focus();
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
            _profileView.DeletionProfileLabel.Text = $"Selected active profile to be deleted: {profileName}";
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

            _profileView.DeletionConfirmButton.Enabled = false;
            _profileView.DeletionConfirmButton.Text = "Deleting...";
            await DeleteActiveUserProfileAsync();
        }

        private void UpdateInlineUserDeletionAvailability()
        {
            bool canDeleteProfile = CanDeleteActiveProfile();
            _profileView.DeletionConfirmButton.Enabled = canDeleteProfile;
            _profileView.DeletionConfirmButton.Text = canDeleteProfile ? "Delete profile" : "Login required";
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
            _profileView.RegistrationQrPictureBox.Image = null;
            _profileView.RegistrationQrPictureBox.Visible = false;
            UpdateUserRegistrationBackButton();
            _profileView.RegistrationStatusLabel.Text = string.Empty;
            _profileView.RegistrationSubmitButton.Text = "Continue";
            _profileView.RegistrationSubmitButton.Enabled = true;
        }

        private void UpdateUserRegistrationBackButton()
        {
            bool hasQrCode = _profileView.RegistrationQrPictureBox.Image != null;
            _profileView.RegistrationUrlTextBox.ReadOnly = hasQrCode;
            _profileView.RegistrationNameTextBox.ReadOnly = hasQrCode;
            _profileView.RegistrationBackButton.Text = hasQrCode ? "Cancel" : "Back";
            _profileView.RegistrationBackButton.Width = 90;
            _profileView.RegistrationSubmitButton.Left = _profileView.RegistrationBackButton.Right + 5;
        }

        private void quickPanelUserRegistrationBackButton_Click(object? sender, EventArgs e)
        {
            bool hasQrCode = _profileView.RegistrationQrPictureBox.Image != null;
            _bootstrapRegistrationService.CancelRegistrationPolling();
            ResetInlineUserRegistration(keepFormOpen: hasQrCode);
            UpdateQuickPanelState();
            if (hasQrCode)
            {
                _profileView.RegistrationNameTextBox.Focus();
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
                _profileView.RegistrationSubmitButton.Enabled = false;
                _profileView.RegistrationStatusLabel.Text = "Generating QR code...";

                await _bootstrapRegistrationService
                    .PrepareRegistrationQrAsync(
                        _profileView.RegistrationUrlTextBox.Text,
                        _profileView.RegistrationNameTextBox.Text)
                    .ConfigureAwait(true);

                Bitmap? qrCode = _bootstrapRegistrationService.QrCode;
                if (qrCode == null)
                {
                    throw new InvalidOperationException("QR code is empty.");
                }

                _profileView.RegistrationQrPictureBox.Image = qrCode;
                _profileView.RegistrationQrPictureBox.Visible = true;
                UpdateUserRegistrationBackButton();
                _inlineUserRegistrationAwaitingCompletion = true;
                _profileView.RegistrationSubmitButton.Text = "QR ready";
                _profileView.RegistrationStatusLabel.Text = "QR generated. Waiting for mobile confirmation.";
                _lastTrayMessage = "User registration QR generated. Waiting for mobile confirmation.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                _bootstrapRegistrationService.StartRegistrationPolling();
            }
            catch (Exception exception)
            {
                _profileView.RegistrationStatusLabel.Text = exception.Message;
            }
            finally
            {
                _inlineUserRegistrationSubmitting = false;
                _profileView.RegistrationSubmitButton.Enabled = !_inlineUserRegistrationAwaitingCompletion;
            }
        }
    }
}
