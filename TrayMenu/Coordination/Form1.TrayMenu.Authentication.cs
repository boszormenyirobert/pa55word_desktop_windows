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
        private readonly LoginControl _loginView = new();

        private void RefreshQuickPanelSelections()
        {
            if (!_isDeviceRegistered)
            {
                return;
            }

            if (_trayMenuController?.ActiveSection == TrayMenuSection.Credentials)
            {
                PopulateCredentialSelectors();
                return;
            }

            _credentialSelectorsFingerprint = string.Empty;
            _updatingQuickPanelSelections = true;
            TrayDropdown profiles = _loginView.ProfileSelector;
            TrayDropdown servers = _loginView.ServerSelector;

            try
            {
                profiles.Items.Clear();
                servers.Items.Clear();

                if (!_registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) ||
                    registrations == null ||
                    registrations.Count == 0)
                {
                    profiles.Enabled = false;
                    servers.Enabled = false;
                    return;
                }

                string? activeUserId = AppEnvironment.GetActiveUserId();
                DesktopRegistration activeRegistration = registrations.FirstOrDefault(registration =>
                        string.Equals(registration.UserKey, activeUserId, StringComparison.Ordinal))
                    ?? registrations[0];

                foreach (DesktopRegistration registration in registrations)
                {
                    profiles.Items.Add(new SelectionOption(registration.DisplayUserName, registration.UserKey));
                }

                SelectComboBoxValue(profiles, activeRegistration.UserKey);
                profiles.Enabled = registrations.Count > 1;

                string? activeNamespaceId = AppEnvironment.GetActiveNamespaceId();
                foreach (NamespaceItemDto namespaceItem in activeRegistration.Namespaces)
                {
                    servers.Items.Add(new SelectionOption(namespaceItem.Namespace, namespaceItem.NamespaceId));
                }

                if (activeRegistration.Namespaces.Count > 0)
                {
                    string selectedNamespaceId = activeRegistration.Namespaces.FirstOrDefault(namespaceItem =>
                            string.Equals(namespaceItem.NamespaceId, activeNamespaceId, StringComparison.Ordinal))?.NamespaceId
                        ?? activeRegistration.Namespaces[0].NamespaceId;
                    SelectComboBoxValue(servers, selectedNamespaceId);
                }

                servers.Enabled = activeRegistration.Namespaces.Count > 1;
            }
            finally
            {
                _updatingQuickPanelSelections = false;
            }
        }

        private void UpdateLoginPresentation(TrayMenuViewState viewState)
        {
            bool active = _trayMenuController?.ActiveSection == TrayMenuSection.Login;
            _loginView.Visible = active;
            if (!active) return;
            bool otherProfileAuthorized = _authenticationService.HasAuthorizedOtherProfile;
            _loginView.ApplyState(new LoginViewState(
                viewState.ShowUserSelector,
                !_authenticationService.IsAuthorized && !otherProfileAuthorized && _loginView.ProfileSelector.Items.Count > 1,
                _authenticationService.IsAuthorizedForActiveNamespace, _authenticationInProgress,
                _authenticationService.CanLoginForActiveNamespace && !_authenticationInProgress,
                _authenticationService.IsAuthorizedForActiveNamespace && !_authenticationInProgress));
            if (otherProfileAuthorized)
                _quickPanelDetailDescriptionLabel.Text = "Only one profile can be signed in at a time. Log out before signing in with another profile.";
        }

        private void HandleLoginProfileChanged(object? sender, EventArgs e)
        {
            TrayDropdown selector = _loginView.ProfileSelector;
            if (_updatingQuickPanelSelections || selector.SelectedItem is not SelectionOption option)
            {
                return;
            }

            AppEnvironment.SetActiveUser(option.Value);
            _credentialsPresenter.InvalidateCollection();
            RefreshQuickPanelSelections();
            ResetDevicesState();
            UpdateQuickPanelState();

            if (_trayMenuController?.ActiveSection == TrayMenuSection.Devices)
            {
                _ = LoadAccountDevicesAsync();
            }
        }

        private void HandleLoginServerChanged(object? sender, EventArgs e)
        {
            TrayDropdown selector = _loginView.ServerSelector;
            if (_updatingQuickPanelSelections || selector.SelectedItem is not SelectionOption option)
            {
                return;
            }

            AppEnvironment.SetActiveNamespace(option.Value);
            _credentialsPresenter.InvalidateCollection();
            ResetDevicesState();
            UpdateQuickPanelState();

            if (_trayMenuController?.ActiveSection == TrayMenuSection.Devices)
            {
                _ = LoadAccountDevicesAsync();
            }
        }

        private async void quickPanelPrimaryActionButton_Click(object? sender, EventArgs e)
        {
            if (_authenticationInProgress)
            {
                return;
            }

            if (_authenticationService.IsAuthorized)
            {
                await LogoutAsync();
                return;
            }

            await LoginAsync();
        }

        private async void quickPanelServerLoginInlineButton_Click(object? sender, EventArgs e)
        {
            if (_authenticationInProgress)
            {
                return;
            }

            if (!_authenticationService.IsAuthorizedForActiveNamespace)
            {
                if (_authenticationService.CanLoginForActiveNamespace)
                {
                    await LoginAsync();
                }

                return;
            }

            try
            {
                _authenticationInProgress = true;
                UpdateAuthenticationMenuItems();
                ShowServerAuthenticationProgress();
                await _authenticationService.LogoutActiveNamespaceAsync().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Logout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _authenticationInProgress = false;
                if (_authenticationService.IsAuthorized)
                {
                    _extensionSessionStateStore.MarkAuthorized();
                }
                else
                {
                    _extensionSessionStateStore.MarkInvalid("authorization_revoked");
                }

                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                UpdateQuickPanelState();
                UpdateAuthenticationMenuItems();
            }
        }

        private async void quickPanelRefreshButton_Click(object? sender, EventArgs e)
        {
            await RefreshQuickPanelFromBackendAsync().ConfigureAwait(true);
        }

        private async Task RefreshQuickPanelFromBackendAsync()
        {
            if (!_isDeviceRegistered || _quickPanelRefreshInProgress)
            {
                return;
            }

            _quickPanelRefreshInProgress = true;
            UpdateQuickPanelState();

            try
            {
                AuthorizationHealthResult result = await _authenticationService.CheckAuthorizationStateAsync().ConfigureAwait(true);

                if (result.Authorized)
                {
                    _extensionSessionStateStore.MarkAuthorized();
                    _lastTrayMessage = "Desktop attestation is active.";
                    _lastTrayMessageIcon = ToolTipIcon.Info;
                }
                else if (result.DeviceRemoved)
                {
                    _extensionSessionStateStore.MarkInvalid(AuthenticationService.DeviceRemovedReason);
                    _lastTrayMessage = AuthenticationService.DeviceRemovedMessage;
                    _lastTrayMessageIcon = ToolTipIcon.Warning;
                }
                else if (result.ExplicitlyInvalid)
                {
                    _authenticationService.RevokeAuthorizationForActiveNamespace();
                    _extensionSessionStateStore.MarkInvalid(result.Reason ?? "authorization_revoked");
                    _lastTrayMessage = "Desktop attestation needs a new mobile approval.";
                    _lastTrayMessageIcon = ToolTipIcon.Warning;
                }
                else if (result.TransientFailure)
                {
                    _lastTrayMessage = "Backend refresh unavailable. Showing cached state.";
                    _lastTrayMessageIcon = ToolTipIcon.Warning;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tray panel refresh failed: {ex.Message}");
                _lastTrayMessage = "Backend refresh unavailable. Showing cached state.";
                _lastTrayMessageIcon = ToolTipIcon.Warning;
            }
            finally
            {
                _quickPanelRefreshInProgress = false;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                UpdateQuickPanelState();

                if (_trayMenuController?.ActiveSection == TrayMenuSection.Credentials)
                {
                    _ = LoadCredentialCollectionAsync(forceRefresh: true);
                }
            }
        }

        private async Task SelectActiveServerAsync(string namespaceId)
        {
            if (_authenticationInProgress || string.IsNullOrWhiteSpace(namespaceId))
            {
                return;
            }

            AppEnvironment.SetActiveNamespace(namespaceId);
            RefreshQuickPanelSelections();
            UpdateQuickPanelState();

            if (!_authenticationService.IsAuthorizedForActiveNamespace && _authenticationService.CanLoginForActiveNamespace)
            {
                await LoginAsync();
                return;
            }

            await RefreshQuickPanelFromBackendAsync().ConfigureAwait(true);
        }
    }
}
