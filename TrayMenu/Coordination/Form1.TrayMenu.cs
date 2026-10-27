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
        private const int QuickPanelGuidanceLeft = 32;
        private const int QuickPanelGuidanceTop = 190;
        private const int QuickPanelGuidanceListTop = 384;
        private const int QuickPanelContentBottomMargin = 15;
        private readonly Panel _quickPanelContainer = new BufferedPanel();
        private readonly SidebarControl _sidebar = new();
        private readonly Panel _quickPanelDividerPanel = new();
        private readonly Panel _quickPanelDetailPanel = new();
        private readonly FaqControl _quickPanelFaqPanel = new();
        private readonly Label _quickPanelDetailEyebrowLabel = new();
        private readonly Label _quickPanelDetailTitleLabel = new();
        private readonly Label _quickPanelDetailDescriptionLabel = new();
        private readonly Font _quickPanelDetailDefaultTitleFont = new("Segoe UI", 24F, FontStyle.Bold);
        private readonly Panel _quickPanelSelectionCard = new();
        private readonly Label _quickPanelHomeActiveUserLabel = new();
        private readonly Label _quickPanelHomeActiveServerLabel = new();
        private readonly Label _quickPanelHomeSessionLabel = new();
        private readonly Panel _quickPanelMetricsCard = new();
        private readonly Panel _quickPanelInsightCard = new();
        private readonly Label _quickPanelInsightTitleLabel = new();
        private readonly Label _quickPanelInsightBodyLabel = new();
        private readonly Button _quickPanelRegisterNewServerInlineButton = new();
        private readonly Button _quickPanelServerRegistrationInlineButton = new();
        private readonly Label _quickPanelServerStatusValueLabel = new();
        private readonly Button _quickPanelServerLoginInlineButton = new();
        private readonly Label _quickPanelAccountValueLabel = new();
        private readonly Label _quickPanelDeviceValueLabel = new();
        private readonly Label _quickPanelConnectionValueLabel = new();
        private readonly Label _quickPanelLastUpdatedValueLabel = new();
        private readonly ToolTip _quickPanelToolTip = new();
        private bool _updatingQuickPanelSelections;
        private bool _quickPanelRefreshInProgress;
        private string? _lastTrayMessage;
        private ToolTipIcon _lastTrayMessageIcon = ToolTipIcon.None;
        private DateTimeOffset _lastTrayStateUpdatedAt = DateTimeOffset.Now;
        private TrayMenuController? _trayMenuController;
        private const int InitialMainWindowHeight = 760;

        private sealed class QuickPanelState
        {
            public DesktopRegistration? Registration { get; init; }

            public NamespaceItemDto? ActiveNamespace { get; init; }

            public string StatusText { get; init; } = string.Empty;

            public Color StatusBackColor { get; init; }

            public Color StatusForeColor { get; init; }

            public string ActiveNamespaceStatusText { get; init; } = string.Empty;

            public Color ActiveNamespaceStatusBackColor { get; init; }

            public Color ActiveNamespaceStatusForeColor { get; init; }
        }

        private void InitializeQuickPanel()
        {
            notifyIcon1.MouseUp += notifyIcon1_MouseUp;

            BuildQuickPanel();
            MainWindowPresentation.AttachContent(this, _quickPanelContainer);
            _quickPanelDetailPanel.SizeChanged += (_, _) => LayoutMainWindowContent();
        }

        private void ApplyQuickPanelLayout()
        {
            MainWindowPresentation.Configure(this);
            _quickPanelContainer.Visible = true;
            SetRegistrationControlsVisible(false);
            LayoutMainWindowContent();
        }

        private void LayoutMainWindowContent()
        {
            LayoutCredentialEntryEditor();
            LayoutSettingsOverview();
            _accountsView.LayoutContent(_quickPanelDetailPanel.Height);
            _devicesView.LayoutContent(_quickPanelDetailPanel.Height);
        }

        private void ShowQuickPanel(bool refreshFromBackend = true)
        {
            if (!_isDeviceRegistered)
            {
                ShowBaseUrlPrompt();
                return;
            }

            ApplyQuickPanelLayout();
            RefreshQuickPanelSelections();
            UpdateQuickPanelState();

            ShowAndActivateWindow();

            if (refreshFromBackend)
            {
                _ = RefreshQuickPanelFromBackendAsync();
            }
        }

        private static void SelectComboBoxValue(TrayDropdown comboBox, string value)
        {
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                if (comboBox.Items[index] is SelectionOption option &&
                    string.Equals(option.Value, value, StringComparison.Ordinal))
                {
                    if (comboBox.SelectedIndex != index)
                    {
                        comboBox.SetSelectedIndexSilently(index);
                    }

                    return;
                }
            }

            if (comboBox.Items.Count > 0 && comboBox.SelectedIndex != 0)
            {
                comboBox.SetSelectedIndexSilently(0);
            }
        }

        private void UpdateQuickPanelState()
        {
            if (!_isDeviceRegistered)
            {
                return;
            }

            if (_trayMenuController?.ActiveSection == TrayMenuSection.Recovery &&
                _authenticationService.IsAuthorized)
            {
                _selectedRecoveryProfileUserId = string.Empty;
                _trayMenuController.SetActiveSection(TrayMenuSection.Settings);
            }

            _quickPanelContainer.SuspendLayout();

            try
            {
                ApplyQuickPanelState(BuildQuickPanelState());
            }
            finally
            {
                _quickPanelContainer.ResumeLayout(performLayout: true);
            }
        }

        private QuickPanelState BuildQuickPanelState()
        {
            GetActiveTraySelection(out DesktopRegistration? registration, out NamespaceItemDto? activeNamespace);
            (string statusText, Color statusBackColor, Color statusForeColor) = ResolveTrayStatus();
            (string activeNamespaceStatusText, Color activeNamespaceStatusBackColor, Color activeNamespaceStatusForeColor) = ResolveActiveNamespaceStatus();

            return new QuickPanelState
            {
                Registration = registration,
                ActiveNamespace = activeNamespace,
                StatusText = statusText,
                StatusBackColor = statusBackColor,
                StatusForeColor = statusForeColor,
                ActiveNamespaceStatusText = activeNamespaceStatusText,
                ActiveNamespaceStatusBackColor = activeNamespaceStatusBackColor,
                ActiveNamespaceStatusForeColor = activeNamespaceStatusForeColor
            };
        }

        private void ApplyQuickPanelState(QuickPanelState state)
        {
            _sidebar.ApplyIdentity(new SidebarIdentityState(
                state.Registration?.DisplayUserName ?? "Not registered", state.StatusText,
                state.StatusBackColor, state.StatusForeColor,
                BuildQuickPanelSummary(state.StatusText, state.ActiveNamespace)));
            _quickPanelServerStatusValueLabel.Text = state.ActiveNamespaceStatusText;
            _quickPanelServerStatusValueLabel.BackColor = state.ActiveNamespaceStatusBackColor;
            _quickPanelServerStatusValueLabel.ForeColor = state.ActiveNamespaceStatusForeColor;
            RefreshQuickPanelServerStatusList(state.Registration);

            _quickPanelAccountValueLabel.Text = state.Registration?.DisplayUserName ?? "Not registered";
            _quickPanelDeviceValueLabel.Text = state.Registration?.DeviceId ?? "Waiting for pairing";
            _quickPanelConnectionValueLabel.Text = BuildConnectionSummary();
            _quickPanelLastUpdatedValueLabel.Text = _quickPanelRefreshInProgress
                ? "Refreshing..."
                : _lastTrayStateUpdatedAt.LocalDateTime.ToString("HH:mm:ss");

            ApplyQuickPanelSectionAvailability();
            ApplyQuickPanelViewState(BuildQuickPanelViewState(
                state.Registration,
                state.ActiveNamespace,
                state.StatusText));
            UpdateAuthenticationMenuItems();
            UpdateTrayTooltip(state.Registration, state.ActiveNamespace, state.StatusText);
        }

        private void GetActiveTraySelection(
            out DesktopRegistration? registration,
            out NamespaceItemDto? activeNamespace)
        {
            registration = null;
            activeNamespace = null;

            if (!_registeredDeviceService.TryReadRegistration(out DesktopRegistration? activeRegistration) || activeRegistration == null)
            {
                return;
            }

            registration = activeRegistration;
            string? activeNamespaceId = AppEnvironment.GetActiveNamespaceId();
            activeNamespace = activeRegistration.Namespaces.FirstOrDefault(namespaceItem =>
                string.Equals(namespaceItem.NamespaceId, activeNamespaceId, StringComparison.Ordinal))
                ?? activeRegistration.Namespaces.FirstOrDefault();
        }

        private (string StatusText, Color BackColor, Color ForeColor) ResolveTrayStatus()
        {
            ExtensionSessionStateResponseDto sessionState = _extensionSessionStateStore.GetSnapshot();

            if (_authenticationInProgress)
            {
                return ("In Progress", Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14));
            }

            if (_isDeviceRegistered && sessionState.Authorized)
            {
                return ("Connected", Color.FromArgb(220, 252, 231), Color.FromArgb(22, 101, 52));
            }

            return ("Disconnected", Color.FromArgb(226, 232, 240), Color.FromArgb(51, 65, 85));
        }

        private (string StatusText, Color BackColor, Color ForeColor) ResolveActiveNamespaceStatus()
        {
            if (_authenticationInProgress)
            {
                return ("In Progress", Color.FromArgb(254, 243, 199), Color.FromArgb(146, 64, 14));
            }

            if (_isDeviceRegistered && _authenticationService.IsAuthorizedForActiveNamespace)
            {
                return ("Connected", Color.FromArgb(220, 252, 231), Color.FromArgb(22, 101, 52));
            }

            return ("Disconnected", Color.FromArgb(226, 232, 240), Color.FromArgb(51, 65, 85));
        }

        private string BuildQuickPanelSummary(string statusText, NamespaceItemDto? activeNamespace)
        {
            return statusText switch
            {
                "Connected" => "Desktop session is active.",
                "In Progress" => "Waiting for confirmation on the paired mobile device.",
                _ => _lastTrayMessageIcon is ToolTipIcon.Warning or ToolTipIcon.Error
                    ? "Desktop session is not active. Recovery may be required."
                    : "Desktop session is not active."
            };
        }

        private string BuildConnectionSummary()
        {
            ExtensionSessionStateResponseDto sessionState = _extensionSessionStateStore.GetSnapshot();

            if (_authenticationInProgress)
            {
                return "In Progress";
            }

            return _isDeviceRegistered && sessionState.Authorized ? "Connected" : "Disconnected";
        }

        private void UpdateTrayTooltip(DesktopRegistration? registration, NamespaceItemDto? activeNamespace, string statusText)
        {
            string userLabel = registration?.DisplayUserName ?? "Not registered";
            string deviceLabel = string.IsNullOrWhiteSpace(registration?.DeviceId) ? "no device" : registration!.DeviceId;
            string serverLabel = activeNamespace?.Namespace ?? "default server";
            string tooltip = $"{statusText} | {userLabel} | {serverLabel} | {deviceLabel}";

            notifyIcon1.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
        }

        private void RefreshQuickPanelServerStatusList(DesktopRegistration? registration)
        {
            _sidebar.RenderServers(BuildTrayServerStatuses(registration));
        }

        private IReadOnlyList<TrayServerStatus> BuildTrayServerStatuses(DesktopRegistration? registration)
        {
            if (registration == null || registration.Namespaces.Count == 0)
            {
                return new[] { new TrayServerStatus(string.Empty, "No servers", false, false) };
            }

            string? activeNamespaceId = AppEnvironment.GetActiveNamespaceId();

            return registration.Namespaces
                .Select(namespaceItem => new TrayServerStatus(
                    namespaceItem.NamespaceId,
                    string.IsNullOrWhiteSpace(namespaceItem.Namespace) ? namespaceItem.Url : namespaceItem.Namespace,
                    _authorizationSessionStore.HasActiveSessionForNamespace(registration.UserKey, namespaceItem.NamespaceId),
                    string.Equals(namespaceItem.NamespaceId, activeNamespaceId, StringComparison.Ordinal)))
                .ToArray();
        }

        private TrayMenuViewState BuildQuickPanelViewState(
            DesktopRegistration? registration,
            NamespaceItemDto? activeNamespace,
            string statusText)
        {
            int registrationCount = _registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) && registrations != null
                ? registrations.Count
                : 0;
            int namespaceCount = registration?.Namespaces.Count ?? 0;
            string serverLabel = activeNamespace?.Namespace ?? "default server";
            string activeUserLabel = registration?.DisplayUserName ?? "No active profile";
            bool hasDesktopSession = _authorizationSessionStore.TryGetState(out AuthorizationSessionStore.AuthorizationSessionState? sessionState) &&
                sessionState != null &&
                sessionState.Active;
            IReadOnlyList<CredentialEntryListItem> filteredCredentialEntries = _credentialsPresenter.GetFilteredEntries();
            var context = new TrayMenuContext
            {
                StatusText = statusText,
                ActiveUserLabel = activeUserLabel,
                ActiveServerLabel = serverLabel,
                DesktopDeviceId = registration?.DeviceId ?? "Waiting for pairing",
                ConnectionSummary = BuildConnectionSummary(),
                LastUpdatedText = _quickPanelLastUpdatedValueLabel.Text,
                RegisteredUserCount = registrationCount,
                RegisteredServerCount = namespaceCount,
                RegisteredUsers = registrations?.Select(item => item.DisplayUserName).ToArray() ?? Array.Empty<string>(),
                CredentialGroupCount = _credentialsPresenter.Groups.Count,
                CredentialEntryCount = _credentialsPresenter.Entries.Count,
                FilteredCredentialEntryCount = filteredCredentialEntries.Count,
                SelectedCredentialGroupLabel = _credentialsPresenter.GetSelectedGroupLabel(),
                CredentialsLoading = _credentialsPresenter.IsLoading,
                CredentialsErrorMessage = _credentialsPresenter.ErrorMessage,
                SessionState = hasDesktopSession ? sessionState : null
            };

            return TrayMenuViews.Build(_trayMenuController?.ActiveSection ?? TrayMenuSection.Login, context);
        }

        private void ApplyQuickPanelViewState(TrayMenuViewState viewState)
        {
            ConfigureSelectionCardLayout(viewState);
            _quickPanelDetailEyebrowLabel.Text = viewState.Eyebrow;
            _quickPanelDetailTitleLabel.Text = viewState.Title;
            _quickPanelDetailDescriptionLabel.Text = viewState.Description;
            _quickPanelInsightTitleLabel.Text = viewState.InsightTitle;
            _quickPanelInsightBodyLabel.Text = viewState.InsightBody;

            bool credentialsLayout = viewState.SelectionLayout == TrayMenuSelectionLayout.CredentialSelectors;
            UpdateCredentialWorkspacePresentation(credentialsLayout);

            if (credentialsLayout)
            {
                PopulateCredentialSelectors();
                RenderCredentialWorkspace();
            }
            else if (viewState.SelectionLayout == TrayMenuSelectionLayout.SettingsUserActions)
            {
                PopulateSettingsUserActions();
            }
            else if (viewState.SelectionLayout == TrayMenuSelectionLayout.SettingsServerActions)
            {
                PopulateSettingsServerActions();
            }
            else
            {
                RefreshQuickPanelSelections();
            }

            UpdateLoginPresentation(viewState);

            _quickPanelHomeActiveUserLabel.Text = viewState.SummaryLines.ElementAtOrDefault(0) ?? string.Empty;
            _quickPanelHomeActiveServerLabel.Text = viewState.SummaryLines.ElementAtOrDefault(1) ?? string.Empty;
            _quickPanelHomeSessionLabel.Text = viewState.SummaryLines.ElementAtOrDefault(2) ?? string.Empty;

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

            UpdateSectionPresentation(viewState);
            UpdateAccountsPresentation();
            UpdateDevicesPresentation();
            UpdateFaqPresentation();
            UpdateSettingsOverviewPresentation(viewState);
            UpdateSettingsViewsPresentation();
        }

        private void ConfigureSelectionCardLayout(TrayMenuViewState viewState)
        {
            bool credentials = viewState.SelectionLayout == TrayMenuSelectionLayout.CredentialSelectors;
            bool summary = viewState.SelectionLayout == TrayMenuSelectionLayout.HomeSummary;
            _credentialSelection.Visible = credentials;
            if (credentials)
            {
                _credentialSelection.LayoutSelectors();
            }
            _quickPanelHomeActiveUserLabel.Visible = summary;
            _quickPanelHomeActiveServerLabel.Visible = summary;
            _quickPanelHomeSessionLabel.Visible = summary;
            _quickPanelRegisterNewServerInlineButton.Visible = false;
            _quickPanelServerStatusValueLabel.Visible = false;
            _quickPanelServerLoginInlineButton.Visible = false;
            _quickPanelSelectionCard.Bounds = new Rectangle(32, 152, 578, summary ? 106 : 74);
            _quickPanelHomeActiveUserLabel.Location = new Point(16, 16);
            _quickPanelHomeActiveServerLabel.Location = new Point(16, 44);
            _quickPanelHomeSessionLabel.Location = new Point(16, 72);
        }

        private sealed class SelectionOption
        {
            public SelectionOption(string label, string value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }

            public string Value { get; }

            public override string ToString() => Label;
        }
    }
}
