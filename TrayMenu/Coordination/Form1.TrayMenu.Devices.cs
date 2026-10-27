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
        private readonly DevicesControl _devicesView = new();
        private bool _updatingDeviceSourceSelection;
        private bool _devicesLoading;
        private int _devicesLoadVersion;
        private bool _devicesLoadedOnce;
        private bool _deviceDeletionInProgress;
        private string _devicesErrorMessage = string.Empty;
        private const string DeviceSourceAllValue = "__all_devices__";
        private string _selectedDeviceSourceValue = DeviceSourceAllValue;
        private string _devicePendingDeletionKey = string.Empty;
        private IReadOnlyList<AccountDeviceServerResult> _accountDeviceServerResults = Array.Empty<AccountDeviceServerResult>();
        private IReadOnlyList<DeviceListItem> _visibleAccountDevices = Array.Empty<DeviceListItem>();
        private string _devicesPresentationFingerprint = string.Empty;

        private async Task LoadAccountDevicesAsync()
        {
            if (_trayMenuController?.ActiveSection != TrayMenuSection.Devices)
            {
                return;
            }

            int loadVersion = ++_devicesLoadVersion;
            string? profileId = AppEnvironment.GetActiveUserId();
            _devicesLoading = true;
            _devicesErrorMessage = string.Empty;
            _devicePendingDeletionKey = string.Empty;
            UpdateDevicesPresentation();
            _quickPanelContainer.Refresh();
            await Task.Yield();

            try
            {
                if (!IsCurrentDeviceLoad(loadVersion, profileId))
                {
                    return;
                }

                if (!_registeredDeviceService.TryReadRegistration(out DesktopRegistration? registration) || registration == null)
                {
                    _accountDeviceServerResults = Array.Empty<AccountDeviceServerResult>();
                    _visibleAccountDevices = Array.Empty<DeviceListItem>();
                    _devicesLoadedOnce = true;
                    RebuildDeviceSourceSelector();
                    return;
                }

                List<NamespaceItemDto> authorizedNamespaces = registration.Namespaces
                    .Where(namespaceItem => _authorizationSessionStore.Matches(
                        registration.UserKey,
                        namespaceItem.AccountId,
                        namespaceItem.DesktopDeviceId,
                        namespaceItem.Url))
                    .ToList();

                if (authorizedNamespaces.Count == 0)
                {
                    _accountDeviceServerResults = Array.Empty<AccountDeviceServerResult>();
                    _visibleAccountDevices = Array.Empty<DeviceListItem>();
                    _devicesLoadedOnce = true;
                    RebuildDeviceSourceSelector();
                    return;
                }

                Task<AccountDeviceServerResult>[] requests = authorizedNamespaces
                    .Select(LoadDevicesForNamespaceAsync)
                    .ToArray();

                AccountDeviceServerResult[] results = await Task.WhenAll(requests).ConfigureAwait(true);
                if (!IsCurrentDeviceLoad(loadVersion, profileId))
                {
                    return;
                }

                _accountDeviceServerResults = results;
                _devicesLoadedOnce = true;
                RebuildDeviceSourceSelector();
                RebuildVisibleAccountDevices();

                AccountDeviceServerResult[] failedResults = _accountDeviceServerResults
                    .Where(result => !string.IsNullOrWhiteSpace(result.ErrorMessage))
                    .ToArray();

                _devicesErrorMessage = failedResults.Length == 0
                    ? string.Empty
                    : string.Join(
                        Environment.NewLine,
                        failedResults.Select(result => $"{result.SourceUrl}: {result.ErrorMessage}"));
            }
            catch (Exception exception)
            {
                if (!IsCurrentDeviceLoad(loadVersion, profileId))
                {
                    return;
                }

                _accountDeviceServerResults = Array.Empty<AccountDeviceServerResult>();
                _visibleAccountDevices = Array.Empty<DeviceListItem>();
                _devicesErrorMessage = exception.Message;
                _devicesLoadedOnce = true;
            }
            finally
            {
                if (IsCurrentDeviceLoad(loadVersion, profileId))
                {
                    _devicesLoading = false;
                    UpdateDevicesPresentation();
                }
            }
        }

        private bool IsCurrentDeviceLoad(int loadVersion, string? profileId)
        {
            return loadVersion == _devicesLoadVersion &&
                string.Equals(profileId, AppEnvironment.GetActiveUserId(), StringComparison.Ordinal);
        }

        private async Task<AccountDeviceServerResult> LoadDevicesForNamespaceAsync(NamespaceItemDto namespaceItem)
        {
            string sourceUrl = NormalizeSourceUrl(namespaceItem.Url);

            try
            {
                IReadOnlyList<AccountDeviceDto> devices = await _accountDeviceService
                    .ReadDevicesAsync(namespaceItem.Url, namespaceItem.AccountId, namespaceItem.DesktopDeviceId)
                    .ConfigureAwait(true);

                return new AccountDeviceServerResult
                {
                    NamespaceItem = namespaceItem,
                    SourceUrl = sourceUrl,
                    Devices = devices
                };
            }
            catch (Exception exception)
            {
                return new AccountDeviceServerResult
                {
                    NamespaceItem = namespaceItem,
                    SourceUrl = sourceUrl,
                    ErrorMessage = exception.Message
                };
            }
        }

        private void quickPanelDevicesSourceComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_updatingDeviceSourceSelection || _devicesView.SourceComboBox.SelectedItem is not SelectionOption option)
            {
                return;
            }

            _selectedDeviceSourceValue = option.Value;
            _devicePendingDeletionKey = string.Empty;
            RebuildVisibleAccountDevices();
            UpdateDevicesPresentation();
        }

        private void RebuildDeviceSourceSelector()
        {
            _updatingDeviceSourceSelection = true;

            try
            {
                _devicesView.SourceComboBox.Items.Clear();
                _devicesView.SourceComboBox.Items.Add(new SelectionOption("All devices", DeviceSourceAllValue));

                foreach (string sourceUrl in _accountDeviceServerResults
                             .Select(result => result.SourceUrl)
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    _devicesView.SourceComboBox.Items.Add(new SelectionOption(sourceUrl, sourceUrl));
                }

                bool selectedValueExists = _devicesView.SourceComboBox.Items
                    .OfType<SelectionOption>()
                    .Any(option => string.Equals(option.Value, _selectedDeviceSourceValue, StringComparison.Ordinal));

                if (!selectedValueExists)
                {
                    _selectedDeviceSourceValue = DeviceSourceAllValue;
                }

                SelectComboBoxValue(_devicesView.SourceComboBox, _selectedDeviceSourceValue);
                _devicesView.SourceComboBox.Enabled = _devicesView.SourceComboBox.Items.Count > 1;
            }
            finally
            {
                _updatingDeviceSourceSelection = false;
            }
        }

        private void RebuildVisibleAccountDevices()
        {
            IEnumerable<DeviceListItem> mappedItems = _accountDeviceServerResults
                .Where(result => string.IsNullOrWhiteSpace(result.ErrorMessage))
                .SelectMany(result => result.Devices.Select(device => new DeviceListItem
                {
                    Device = device,
                    NamespaceItem = result.NamespaceItem,
                    SourceUrl = result.SourceUrl,
                    IdentityKey = BuildDeviceIdentityKey(device, result.NamespaceItem.NamespaceId, result.SourceUrl)
                }));

            if (!string.Equals(_selectedDeviceSourceValue, DeviceSourceAllValue, StringComparison.Ordinal))
            {
                mappedItems = mappedItems.Where(item =>
                    string.Equals(item.SourceUrl, _selectedDeviceSourceValue, StringComparison.OrdinalIgnoreCase));
            }

            _visibleAccountDevices = mappedItems
                .OrderByDescending(item => item.Device.LastTimeUsed ?? DateTimeOffset.MinValue)
                .ThenBy(item => item.Device.DeviceName ?? item.IdentityKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void ResetDevicesState()
        {
            _devicesLoadVersion++;
            _devicesLoading = false;
            _devicesLoadedOnce = false;
            _devicesErrorMessage = string.Empty;
            _selectedDeviceSourceValue = DeviceSourceAllValue;
            _devicePendingDeletionKey = string.Empty;
            _accountDeviceServerResults = Array.Empty<AccountDeviceServerResult>();
            _visibleAccountDevices = Array.Empty<DeviceListItem>();
            RebuildDeviceSourceSelector();
        }

        private static string NormalizeSourceUrl(string? sourceUrl)
        {
            return string.IsNullOrWhiteSpace(sourceUrl)
                ? "Unknown server URL"
                : sourceUrl.TrimEnd('/');
        }

        private static string BuildDeviceIdentityKey(AccountDeviceDto device, string namespaceId, string sourceUrl)
        {
            string scope = $"{namespaceId}|{sourceUrl}";

            if (!string.IsNullOrWhiteSpace(device.DesktopDeviceId))
            {
                return $"{scope}|desktop:{device.DesktopDeviceId}";
            }

            if (!string.IsNullOrWhiteSpace(device.MobileDeviceId))
            {
                return $"{scope}|mobile:{device.MobileDeviceId}";
            }

            return $"{scope}|name:{device.DeviceName?.Trim() ?? "unknown"}";
        }

        private void UpdateDevicesPresentation()
        {
            bool active = _trayMenuController?.ActiveSection == TrayMenuSection.Devices;
            _devicesView.Visible = active;
            _quickPanelDetailTitleLabel.AutoSize = !active;
            _quickPanelDetailTitleLabel.Font = active ? _devicesView.TitleFont : _quickPanelDetailDefaultTitleFont;
            if (!active)
            {
                return;
            }

            _quickPanelDetailTitleLabel.Size = new Size(550, 40);
            _quickPanelDetailDescriptionLabel.Size = new Size(574, 44);
            _devicesView.LayoutContent(_quickPanelDetailPanel.Height);
            _quickPanelSelectionCard.Visible = false;
            _quickPanelMetricsCard.Visible = false;
            _quickPanelInsightCard.Visible = false;
            _devicesView.Render(_devicesLoading, _devicesLoadedOnce, _accountDeviceServerResults.Count > 0,
                _devicesErrorMessage, _visibleAccountDevices, _devicePendingDeletionKey, _deviceDeletionInProgress);
        }

        private async Task DeleteDeviceFromQuickPanelAsync(DeviceListItem item)
        {
            if (_deviceDeletionInProgress)
            {
                return;
            }

            bool deletedSigningDevice = string.Equals(
                item.Device.DesktopDeviceId,
                item.NamespaceItem.DesktopDeviceId,
                StringComparison.Ordinal);
            _deviceDeletionInProgress = true;
            UpdateDevicesPresentation();

            try
            {
                await _accountDeviceService.DeleteDeviceAsync(
                    item.Device,
                    item.NamespaceItem.Url,
                    item.NamespaceItem.AccountId,
                    item.NamespaceItem.DesktopDeviceId).ConfigureAwait(true);
                _devicePendingDeletionKey = string.Empty;

                if (deletedSigningDevice)
                {
                    RemoveDeletedSigningDeviceRegistration(item.NamespaceItem);
                    return;
                }

                await LoadAccountDevicesAsync();
            }
            catch (Exception exception)
            {
                _devicePendingDeletionKey = string.Empty;
                _devicesErrorMessage = exception.Message;
            }
            finally
            {
                _deviceDeletionInProgress = false;

                if (!deletedSigningDevice)
                {
                    UpdateDevicesPresentation();
                }
            }
        }

        private void RemoveDeletedSigningDeviceRegistration(NamespaceItemDto deletedNamespace)
        {
            string? activeUserId = AppEnvironment.GetActiveUserId();
            var configStore = new RegistrationConfigStore();
            if (string.IsNullOrWhiteSpace(activeUserId) || string.IsNullOrWhiteSpace(deletedNamespace.NamespaceId))
            {
                throw new InvalidOperationException("The deleted device could not be removed from local configuration.");
            }

            configStore.RemoveNamespace(activeUserId, deletedNamespace.NamespaceId);
            _authorizationSessionStore.Revoke(activeUserId, deletedNamespace.NamespaceId);

            bool noProfilesRemain = !configStore.TryReadRegistrations(out List<DesktopRegistration>? registrations) ||
                registrations is not { Count: > 0 };

            if (noProfilesRemain)
            {
                _authenticationService.RevokeAuthorization();
                _extensionSessionStateStore.MarkInvalid("device_deleted");
                AppEnvironment.ClearActiveSelection();
                _isDeviceRegistered = false;
            }
            else
            {
                _extensionSessionStateStore.MarkInvalid("authorization_revoked");

                bool activeNamespaceWasRemoved = string.Equals(
                    AppEnvironment.GetActiveNamespaceId(),
                    deletedNamespace.NamespaceId,
                    StringComparison.Ordinal);

                if (activeNamespaceWasRemoved)
                {
                    _credentialsPresenter.InvalidateCollection();
                    _credentialsPresenter.SelectedGroupId = string.Empty;
                    _credentialsPresenter.SelectedEntryId = string.Empty;
                    _credentialSelectorsFingerprint = string.Empty;
                    _trayMenuController?.SetActiveSection(TrayMenuSection.Login, triggerStateChanged: true);
                }

                RefreshQuickPanelSelections();
                ResetDevicesState();
                _ = LoadAccountDevicesAsync();
            }

            if (noProfilesRemain)
            {
                MessageBox.Show(
                    "The current profile was removed because it no longer has a registered server.",
                    "Profile removed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                CloseQuickPanel();
            }

            _lastTrayStateUpdatedAt = DateTimeOffset.Now;
            UpdateQuickPanelState();
            UpdateAuthenticationMenuItems();
        }

        private sealed class AccountDeviceServerResult
        {
            public NamespaceItemDto NamespaceItem { get; init; } = new();

            public string SourceUrl { get; init; } = string.Empty;

            public IReadOnlyList<AccountDeviceDto> Devices { get; init; } = Array.Empty<AccountDeviceDto>();

            public string ErrorMessage { get; init; } = string.Empty;
        }
    }
}
