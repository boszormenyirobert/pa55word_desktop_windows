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
        private readonly SettingsControl _settingsView = new();
        private readonly ProfileControl _profileView = new();
        private readonly ManageDatabasesControl _databaseView = new();
        private readonly DevicesControl _devicesView = new();
        private readonly LoginControl _loginView = new();
        private readonly CredentialSelectionControl _credentialSelection = new();
        private readonly AccountsControl _accountsView = new();
        private bool _showInlineAccountRename;
        private string? _accountRenameUserId;
        private string _settingsAccessMessage = string.Empty;
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
        private readonly CredentialWorkspaceControl _credentialWorkspace = new();
        private readonly CredentialListPanel _quickPanelCredentialListPanel = new();
        private readonly PanelCollapseIcon _quickPanelCredentialListCollapseButton = new();
        private bool _updatingQuickPanelSelections;
        private bool _quickPanelRefreshInProgress;
        private string? _lastTrayMessage;
        private ToolTipIcon _lastTrayMessageIcon = ToolTipIcon.None;
        private DateTimeOffset _lastTrayStateUpdatedAt = DateTimeOffset.Now;
        private readonly CredentialCollectionService _credentialCollectionService;
        private TrayMenuController? _trayMenuController;
        private const int InitialMainWindowHeight = 760;
        private bool _credentialsLoading;
        private bool _credentialLoadDeferredUntilBackendRefresh;
        private string _credentialsErrorMessage = string.Empty;
        private string _selectedCredentialGroupId = string.Empty;
        private string _selectedCredentialEntryId = string.Empty;
        private string _credentialCollectionCacheKey = string.Empty;
        private bool _credentialCollectionLoaded;
        private bool _credentialListCollapsed = true;
        private bool _credentialDetailLoading;
        private int _credentialDetailLoadRequestVersion;
        private CredentialWorkspaceMode _credentialWorkspaceMode = CredentialWorkspaceMode.ViewEntry;
        private bool _showInlineServerRegistration;
        private bool _inlineServerRegistrationSubmitting;
        private bool _inlineServerRegistrationAwaitingCompletion;
        private InlineServerRegistrationMode _inlineServerRegistrationMode;
        private bool _showInlineUserRegistration;
        private bool _showInlineUserDeletion;
        private bool _inlineUserRegistrationSubmitting;
        private bool _inlineUserRegistrationAwaitingCompletion;
        private bool _updatingCredentialWorkspaceControls;
        private bool _updatingDeviceSourceSelection;
        private bool _devicesLoading;
        private bool _devicesLoadedOnce;
        private bool _deviceDeletionInProgress;
        private string _devicesErrorMessage = string.Empty;
        private const string DeviceSourceAllValue = "__all_devices__";
        private string _selectedDeviceSourceValue = DeviceSourceAllValue;
        private string _devicePendingDeletionKey = string.Empty;
        private IReadOnlyList<AccountDeviceServerResult> _accountDeviceServerResults = Array.Empty<AccountDeviceServerResult>();
        private IReadOnlyList<DeviceListItem> _visibleAccountDevices = Array.Empty<DeviceListItem>();
        private string _devicesPresentationFingerprint = string.Empty;
        private CredentialDeletionConfirmation _deletionConfirmation;
        private string _credentialEditorStatusMessage = string.Empty;
        private string _groupEditorStatusMessage = string.Empty;
        private string _serverRegistrationStatusMessage = string.Empty;
        private string _selectedManagedGroupId = string.Empty;
        private IReadOnlyList<CredentialGroupDto> _credentialGroups = Array.Empty<CredentialGroupDto>();
        private IReadOnlyList<CredentialEntryListItem> _credentialEntries = Array.Empty<CredentialEntryListItem>();
        private readonly Dictionary<int, CredentialEntryDetail> _credentialEntryDetails = new();
        private string _credentialSelectorsFingerprint = string.Empty;

        private enum CredentialWorkspaceMode
        {
            ViewEntry,
            CreateEntry,
            ManageGroup,
            CreateGroup
        }

        private enum CredentialDeletionConfirmation
        {
            None,
            Credential,
            Group
        }

        private bool IsDeletionConfirmationPending(CredentialDeletionConfirmation confirmation)
        {
            return _deletionConfirmation == confirmation;
        }

        private enum InlineServerRegistrationMode
        {
            NamespaceSync,
            NewServer
        }


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

        private sealed class AccountDeviceServerResult
        {
            public NamespaceItemDto NamespaceItem { get; init; } = new();

            public string SourceUrl { get; init; } = string.Empty;

            public IReadOnlyList<AccountDeviceDto> Devices { get; init; } = Array.Empty<AccountDeviceDto>();

            public string ErrorMessage { get; init; } = string.Empty;
        }

        private void InitializeQuickPanel()
        {
            notifyIcon1.MouseUp += notifyIcon1_MouseUp;

            BuildQuickPanel();
            MainWindowPresentation.AttachContent(this, _quickPanelContainer);
            _quickPanelDetailPanel.SizeChanged += (_, _) => LayoutMainWindowContent();
        }

        private void BuildQuickPanel()
        {
            SuspendLayout();

            _quickPanelContainer.Padding = new Padding(0);
            _quickPanelContainer.Margin = Padding.Empty;
            _quickPanelContainer.Location = Point.Empty;
            _quickPanelContainer.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelContainer.Visible = false;
            _quickPanelContainer.Size = new Size(820, InitialMainWindowHeight);

            _quickPanelDividerPanel.BackColor = Color.FromArgb(59, 61, 88);
            _quickPanelDividerPanel.Location = new Point(176, 0);
            _quickPanelDividerPanel.Size = new Size(1, InitialMainWindowHeight);
            _quickPanelDividerPanel.Dock = DockStyle.Left;

            _quickPanelDetailPanel.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelDetailPanel.Location = new Point(177, 0);
            _quickPanelDetailPanel.Size = new Size(643, InitialMainWindowHeight);
            _quickPanelDetailPanel.Dock = DockStyle.Fill;

            _sidebar.SettingsRequested += settingsNavButton_Click;
            _sidebar.PrimaryRequested += quickPanelPrimaryActionButton_Click;
            _sidebar.RefreshRequested += quickPanelRefreshButton_Click;
            _sidebar.ServerSelected += async (_, request) => await SelectActiveServerAsync(request.NamespaceId);
            _trayMenuController = new TrayMenuController(SidebarControl.SetNavigationState, HandleTrayMenuSectionChanged);
            _trayMenuController.Register(TrayMenuSection.Login, _sidebar.LoginNavButton);
            _trayMenuController.Register(TrayMenuSection.Credentials, _sidebar.CredentialsNavButton);
            _trayMenuController.Register(TrayMenuSection.Faq, _sidebar.FaqNavButton);

            _quickPanelDetailEyebrowLabel.AutoSize = true;
            _quickPanelDetailEyebrowLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelDetailEyebrowLabel.ForeColor = Color.FromArgb(120, 205, 0);
            _quickPanelDetailEyebrowLabel.Location = new Point(34, 22);
            _quickPanelDetailEyebrowLabel.Text = "SYSTEM STATUS";

            _quickPanelDetailTitleLabel.AutoSize = true;
            _quickPanelDetailTitleLabel.Font = _quickPanelDetailDefaultTitleFont;
            _quickPanelDetailTitleLabel.ForeColor = Color.White;
            _quickPanelDetailTitleLabel.Location = new Point(32, 52);
            _quickPanelDetailTitleLabel.Text = "Tray overview";

            _quickPanelDetailDescriptionLabel.AutoSize = false;
            _quickPanelDetailDescriptionLabel.Font = new Font("Segoe UI", 10F);
            _quickPanelDetailDescriptionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelDetailDescriptionLabel.Location = new Point(36, 98);
            _quickPanelDetailDescriptionLabel.Size = new Size(574, 44);

            _quickPanelSelectionCard.BackColor = Color.FromArgb(41, 43, 71);
            _quickPanelSelectionCard.BorderStyle = BorderStyle.None;
            _quickPanelSelectionCard.Location = new Point(32, 152);
            _quickPanelSelectionCard.Size = new Size(578, 126);

            _credentialSelection.GroupChanged += quickPanelUserComboBox_SelectedIndexChanged;
            _credentialSelection.EntryChanged += quickPanelNamespaceComboBox_SelectedIndexChanged;
            _credentialSelection.ManageGroupsRequested += quickPanelCredentialManageGroupsButton_Click;
            _credentialSelection.CreateRequested += quickPanelCredentialCreateButton_Click;

            _quickPanelSelectionCard.Controls.Add(_quickPanelRegisterNewServerInlineButton);
            _quickPanelSelectionCard.Controls.Add(_quickPanelServerRegistrationInlineButton);
            _quickPanelSelectionCard.Controls.Add(_quickPanelServerStatusValueLabel);
            _quickPanelSelectionCard.Controls.Add(_quickPanelServerLoginInlineButton);

            InitializeSettingsOverviewPanel();

            ConfigureQuickPanelSummaryValueLabel(_quickPanelHomeActiveUserLabel);
            ConfigureQuickPanelSummaryValueLabel(_quickPanelHomeActiveServerLabel);
            ConfigureQuickPanelSummaryValueLabel(_quickPanelHomeSessionLabel);

            _quickPanelSelectionCard.Controls.Add(_quickPanelHomeActiveUserLabel);
            _quickPanelSelectionCard.Controls.Add(_quickPanelHomeActiveServerLabel);
            _quickPanelSelectionCard.Controls.Add(_quickPanelHomeSessionLabel);

            _quickPanelMetricsCard.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelMetricsCard.BorderStyle = BorderStyle.None;
            _quickPanelMetricsCard.Location = new Point(32, 294);
            _quickPanelMetricsCard.Size = new Size(578, 120);

            AddMetricRow(_quickPanelMetricsCard, 14, "Account", _quickPanelAccountValueLabel);
            AddMetricRow(_quickPanelMetricsCard, 44, "Device", _quickPanelDeviceValueLabel);
            AddMetricRow(_quickPanelMetricsCard, 74, "Connection", _quickPanelConnectionValueLabel);
            AddMetricRow(_quickPanelMetricsCard, 104, "Updated", _quickPanelLastUpdatedValueLabel);

            _quickPanelInsightCard.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelInsightCard.BorderStyle = BorderStyle.None;
            _quickPanelInsightCard.Location = new Point(32, 424);
            _quickPanelInsightCard.Size = new Size(578, 110);

            _quickPanelInsightTitleLabel.AutoSize = true;
            _quickPanelInsightTitleLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            _quickPanelInsightTitleLabel.ForeColor = Color.White;
            _quickPanelInsightTitleLabel.Location = new Point(16, 16);
            _quickPanelInsightTitleLabel.Text = "Overview";

            _quickPanelInsightBodyLabel.AutoSize = false;
            _quickPanelInsightBodyLabel.Font = new Font("Segoe UI", 9.5F);
            _quickPanelInsightBodyLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelInsightBodyLabel.Location = new Point(16, 42);
            _quickPanelInsightBodyLabel.Size = new Size(544, 56);

            InitializeDatabaseView();

            TrayControlStyle.ConfigureInlineActionButton(_quickPanelRegisterNewServerInlineButton, "+ Add a database", new Point(16, 14), quickPanelRegisterNewServerInlineButton_Click);
            _quickPanelRegisterNewServerInlineButton.Size = new Size(156, 28);
            _quickPanelRegisterNewServerInlineButton.ForeColor = Color.FromArgb(120, 205, 0);

            TrayControlStyle.ConfigureInlineActionButton(_quickPanelServerRegistrationInlineButton, "+ Grant database access", new Point(180, 14), quickPanelServerRegistrationInlineButton_Click);
            _quickPanelServerRegistrationInlineButton.Size = new Size(190, 28);
            _quickPanelServerRegistrationInlineButton.ForeColor = Color.FromArgb(120, 205, 0);

            _quickPanelServerStatusValueLabel.AutoSize = false;
            _quickPanelServerStatusValueLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelServerStatusValueLabel.Location = new Point(214, 16);
            _quickPanelServerStatusValueLabel.Size = new Size(108, 24);
            _quickPanelServerStatusValueLabel.TextAlign = ContentAlignment.MiddleCenter;
            _quickPanelServerStatusValueLabel.Visible = false;

            TrayControlStyle.ConfigureInlineActionButton(_quickPanelServerLoginInlineButton, "Login", new Point(438, 14), quickPanelServerLoginInlineButton_Click);
            _quickPanelServerLoginInlineButton.Size = new Size(122, 28);
            _quickPanelServerLoginInlineButton.Visible = false;

            _quickPanelInsightCard.Controls.Add(_quickPanelInsightTitleLabel);
            _quickPanelInsightCard.Controls.Add(_quickPanelInsightBodyLabel);

            InitializeCredentialWorkspacePanel();

            _quickPanelDetailPanel.Controls.Add(_quickPanelDetailEyebrowLabel);
            _quickPanelDetailPanel.Controls.Add(_quickPanelDetailTitleLabel);
            _quickPanelDetailPanel.Controls.Add(_quickPanelDetailDescriptionLabel);
            _quickPanelDetailPanel.Controls.Add(_quickPanelSelectionCard);
            _quickPanelDetailPanel.Controls.Add(_quickPanelMetricsCard);
            _quickPanelDetailPanel.Controls.Add(_quickPanelInsightCard);
            _quickPanelDetailPanel.Controls.Add(_settingsView);
            _quickPanelDetailPanel.Controls.Add(_credentialWorkspace);
            _quickPanelDetailPanel.Controls.Add(_profileView);
            _quickPanelDetailPanel.Controls.Add(_databaseView);
            _quickPanelDetailPanel.Controls.Add(_loginView);
            _quickPanelDetailPanel.Controls.Add(_credentialSelection);
            _loginView.ProfileChanged += (_, args) => quickPanelUserComboBox_SelectedIndexChanged(_loginView.ProfileSelector, args);
            _loginView.ServerChanged += (_, args) => quickPanelNamespaceComboBox_SelectedIndexChanged(_loginView.ServerSelector, args);
            _loginView.LoginRequested += quickPanelServerLoginInlineButton_Click;

            _quickPanelDetailPanel.Controls.Add(_accountsView);
            _accountsView.RemoveRequested += async (_, request) =>
                await DeleteAccountFromQuickPanelAsync(request.UserId, request.NamespaceItem);

            _devicesView.SourceChanged += quickPanelDevicesSourceComboBox_SelectedIndexChanged;
            _devicesView.DeletionConfirmationRequested += (_, item) =>
            {
                _devicePendingDeletionKey = item.IdentityKey;
                UpdateDevicesPresentation();
            };
            _devicesView.DeletionCancelled += (_, _) =>
            {
                _devicePendingDeletionKey = string.Empty;
                UpdateQuickPanelState();
            };
            _devicesView.DeleteRequested += async (_, item) => await DeleteDeviceFromQuickPanelAsync(item);

            _quickPanelDetailPanel.Controls.Add(_devicesView);

            _quickPanelDetailPanel.Controls.Add(_quickPanelFaqPanel);

            _quickPanelContainer.Controls.Add(_quickPanelDetailPanel);
            _quickPanelCredentialListCollapseButton.Click += quickPanelCredentialListCollapseButton_Click;
            _quickPanelCredentialListCollapseButton.Visible = false;
            _quickPanelCredentialListPanel.Visible = false;
            _quickPanelCredentialListPanel.EntrySelected += SelectCredentialFromList;
            _quickPanelCredentialListPanel.SizeChanged += (_, _) => LayoutCredentialListCollapseButton();
            _quickPanelContainer.SizeChanged += (_, _) => LayoutCredentialListCollapseButton();
            _quickPanelContainer.Controls.Add(_quickPanelCredentialListCollapseButton);
            _quickPanelContainer.Controls.Add(_quickPanelCredentialListPanel);
            _quickPanelContainer.Controls.Add(_quickPanelDividerPanel);
            _quickPanelContainer.Controls.Add(_sidebar);

            ResumeLayout(performLayout: false);
        }

        private void InitializeSettingsOverviewPanel()
        {
            _settingsView.NavigationRequested += async (_, request) => await NavigateFromSettingsOverviewAsync(request.Section);
            InitializeAccountRenameControls();
            InitializeRecoveryPanel();
        }

        private void settingsNavButton_Click(object? sender, EventArgs e)
        {
            _settingsAccessMessage = string.Empty;
            _trayMenuController?.SetActiveSection(TrayMenuSection.Settings, triggerStateChanged: true);
        }

        private async Task NavigateFromSettingsOverviewAsync(TrayMenuSection section)
        {
            if (section == TrayMenuSection.Recovery && _authenticationService.IsAuthorized)
            {
                return;
            }

            if (RequiresSettingsAuthentication(section) && !_authenticationService.IsAuthorized)
            {
                ShowSettingsAccessMessage("Log in to access Servers, Devices, and Account settings.");
                return;
            }

            if (section != TrayMenuSection.Devices)
            {
                _settingsAccessMessage = string.Empty;
                if (section == TrayMenuSection.Recovery)
                {
                    PopulateRecoveryProfiles();
                }

                _trayMenuController?.SetActiveSection(section, triggerStateChanged: true);
                return;
            }

            _settingsAccessMessage = string.Empty;
            _trayMenuController?.SetActiveSection(TrayMenuSection.Devices, triggerStateChanged: true);
            await Task.Yield();
        }

        private static bool RequiresSettingsAuthentication(TrayMenuSection section)
        {
            return section is TrayMenuSection.SettingsServers or
                TrayMenuSection.Devices or
                TrayMenuSection.Accounts;
        }

        private void ShowSettingsAccessMessage(string message)
        {
            _settingsAccessMessage = message;
            _trayMenuController?.SetActiveSection(TrayMenuSection.Settings, triggerStateChanged: true);
        }

        private static void ConfigureQuickPanelSummaryValueLabel(Label label)
        {
            label.AutoSize = false;
            label.Font = new Font("Segoe UI", 10F);
            label.ForeColor = Color.White;
            label.Size = new Size(544, 22);
            label.Visible = false;
        }

        private static void AddMetricRow(Control parent, int y, string caption, Label valueLabel)
        {
            var captionLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(171, 174, 211),
                Location = new Point(16, y),
                Text = caption
            };

            valueLabel.AutoSize = false;
            valueLabel.Font = new Font("Segoe UI", 9F);
            valueLabel.ForeColor = Color.White;
            valueLabel.Location = new Point(132, y - 1);
            valueLabel.Size = new Size(428, 18);
            valueLabel.TextAlign = ContentAlignment.MiddleRight;

            parent.Controls.Add(captionLabel);
            parent.Controls.Add(valueLabel);
        }

        private void InitializeDatabaseView()
        {
            _databaseView.ActionChanged += quickPanelSettingsServerActionComboBox_SelectedIndexChanged;
            _databaseView.SubmitRequested += quickPanelServerRegistrationSubmitButton_Click;
            _databaseView.BackRequested += quickPanelServerRegistrationBackButton_Click;
            _databaseView.RestartRequested += quickPanelServerRegistrationRestartButton_Click;
        }

        private void InitializeCredentialWorkspacePanel()
        {
            _credentialWorkspace.BackRequested += quickPanelCredentialBackButton_Click;
            _credentialWorkspace.UsernameCopyRequested += quickPanelCredentialUsernameCopyButton_Click;
            _credentialWorkspace.PasswordGenerateRequested += quickPanelCredentialPasswordGenerateButton_Click;
            _credentialWorkspace.PasswordRevealRequested += quickPanelCredentialPasswordRevealButton_Click;
            _credentialWorkspace.PasswordCopyRequested += quickPanelCredentialPasswordCopyButton_Click;
            _credentialWorkspace.DescriptionCopyRequested += quickPanelCredentialDescriptionCopyButton_Click;
            _credentialWorkspace.DeleteRequested += quickPanelCredentialDeleteButton_Click;
            _credentialWorkspace.SaveRequested += quickPanelCredentialSaveButton_Click;
            _credentialWorkspace.ManagedGroupChanged += quickPanelManagedGroupComboBox_SelectedIndexChanged;
            _credentialWorkspace.GroupDeleteRequested += quickPanelCredentialGroupDeleteButton_Click;
            _credentialWorkspace.GroupSaveRequested += quickPanelCredentialGroupSaveButton_Click;
            _credentialWorkspace.GroupCreateRequested += quickPanelCredentialGroupCreateButton_Click;
            _credentialWorkspace.EntryInputChanged += credentialEditorInput_TextChanged;
            _credentialWorkspace.EntrySelectionChanged += credentialEditorInput_Changed;
            _credentialWorkspace.GroupInputChanged += groupEditorInput_TextChanged;
        }

        private void ApplyRegistrationLayout()
        {
            CloseQuickPanel();
            _quickPanelContainer.Visible = false;
            restartRegistrationButton.Visible = false;
            SetRegistrationControlsVisible(true);
            ApplyRegistrationTheme();
            MainWindowPresentation.ConfigureRegistration(this);
        }

        private void ApplyRegistrationTheme()
        {
            BackColor = Color.FromArgb(31, 33, 57);
            ForeColor = Color.White;

            device_registration.ForeColor = Color.White;
            device_registration.Font = new Font("Segoe UI", 24F, FontStyle.Bold, GraphicsUnit.Point, 0);

            baseUrlLabel.ForeColor = Color.FromArgb(171, 174, 211);
            baseUrlLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            namespaceNameLabel.ForeColor = Color.FromArgb(171, 174, 211);
            namespaceNameLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);

            baseUrlTextBox.BorderStyle = BorderStyle.FixedSingle;
            baseUrlTextBox.BackColor = Color.FromArgb(41, 43, 71);
            baseUrlTextBox.ForeColor = Color.White;
            baseUrlTextBox.Font = new Font("Segoe UI", 10F);

            namespaceNameTextBox.BorderStyle = BorderStyle.FixedSingle;
            namespaceNameTextBox.BackColor = Color.FromArgb(41, 43, 71);
            namespaceNameTextBox.ForeColor = Color.White;
            namespaceNameTextBox.Font = new Font("Segoe UI", 10F);

            continueRegistrationButton.FlatStyle = FlatStyle.Flat;
            continueRegistrationButton.FlatAppearance.BorderSize = 0;
            continueRegistrationButton.BackColor = Color.FromArgb(120, 205, 0);
            continueRegistrationButton.ForeColor = Color.FromArgb(24, 30, 24);
            continueRegistrationButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            continueRegistrationButton.UseVisualStyleBackColor = false;

            restartRegistrationButton.FlatStyle = FlatStyle.Flat;
            restartRegistrationButton.FlatAppearance.BorderColor = Color.FromArgb(101, 106, 146);
            restartRegistrationButton.BackColor = Color.FromArgb(67, 72, 106);
            restartRegistrationButton.ForeColor = Color.White;
            restartRegistrationButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            restartRegistrationButton.UseVisualStyleBackColor = false;

            pictureBoxQr.BackColor = Color.FromArgb(41, 43, 71);
            pictureBoxQr.BorderStyle = BorderStyle.FixedSingle;
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

        private void LayoutCredentialEntryEditor()
        {
            _credentialWorkspace.LayoutEditor(_quickPanelDetailPanel.Height);
        }

        private void LayoutSettingsOverview()
        {
            _settingsView.LayoutContent(_quickPanelDetailPanel.Height,
                _trayMenuController?.ActiveSection is TrayMenuSection.Settings or TrayMenuSection.Recovery);
        }

        private void SetRegistrationControlsVisible(bool visible)
        {
            pictureBoxQr.Visible = visible && pictureBoxQr.Visible;
            device_registration.Visible = visible;
            baseUrlLabel.Visible = visible && baseUrlLabel.Visible;
            baseUrlTextBox.Visible = visible && baseUrlTextBox.Visible;
            continueRegistrationButton.Visible = visible && continueRegistrationButton.Visible;
            namespaceNameLabel.Visible = visible && namespaceNameLabel.Visible;
            namespaceNameTextBox.Visible = visible && namespaceNameTextBox.Visible;
            restartRegistrationButton.Visible = visible && restartRegistrationButton.Visible;
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

        private void HandleTrayMenuSectionChanged()
        {
            TrayMenuSection? activeSection = _trayMenuController?.ActiveSection;

            if (activeSection != TrayMenuSection.Credentials)
            {
                RefreshQuickPanelSelections();
            }

            UpdateQuickPanelState();

            if (activeSection == TrayMenuSection.Credentials)
            {
                if (_authenticationService.IsAuthorizedForActiveNamespace)
                {
                    _ = EnsureCredentialCollectionLoadedAsync(forceRefresh: true);
                }

                return;
            }

            if (activeSection == TrayMenuSection.Devices && !_devicesLoadedOnce)
            {
                _ = LoadAccountDevicesAsync();
            }
        }

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

        private void ApplyQuickPanelSectionAvailability()
        {
            bool hasAuthenticatedUser = _authenticationService.IsAuthorized;
            bool credentialsSectionEnabled = _authenticationService.IsAuthorizedForActiveNamespace;

            if (!credentialsSectionEnabled && _trayMenuController?.ActiveSection == TrayMenuSection.Credentials)
            {
                _trayMenuController.SetActiveSection(TrayMenuSection.Login);
            }

            if (!hasAuthenticatedUser && _trayMenuController?.ActiveSection is
                TrayMenuSection.SettingsServers or
                TrayMenuSection.Devices or
                TrayMenuSection.Accounts or
                TrayMenuSection.Servers)
            {
                _settingsAccessMessage = "Log in to access Servers, Devices, and Account settings.";
                _trayMenuController.SetActiveSection(TrayMenuSection.Settings);
            }

            bool settingsSectionActive = (_trayMenuController?.ActiveSection is
                TrayMenuSection.Settings or
                TrayMenuSection.SettingsUsers or
                TrayMenuSection.SettingsServers or
                TrayMenuSection.Devices or
                TrayMenuSection.Accounts) ||
                _trayMenuController?.ActiveSection == TrayMenuSection.Servers && _showInlineServerRegistration;
            _sidebar.ApplyNavigation(new SidebarNavigationState(_isDeviceRegistered, credentialsSectionEnabled,
                _trayMenuController?.ActiveSection, settingsSectionActive));
        }

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

        private async void quickPanelRefreshButton_Click(object? sender, EventArgs e)
        {
            await RefreshQuickPanelFromBackendAsync().ConfigureAwait(true);
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

        private void notifyIcon1_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenCredentialsFromTray();
            }
        }

        private void OpenCredentialsFromTray()
        {
            if (_isDeviceRegistered)
            {
                _trayMenuController?.SetInitialSection(_authenticationService.IsAuthorizedForActiveNamespace);
                ShowQuickPanel(refreshFromBackend: false);
                HandleTrayMenuSectionChanged();
                return;
            }

            ShowBaseUrlPrompt();
        }

        private void UpdateSettingsOverviewPresentation(TrayMenuViewState viewState)
        {
            TrayMenuSection? activeSection = _trayMenuController?.ActiveSection;
            bool settingsNavigationActive = activeSection is
                TrayMenuSection.Settings or
                TrayMenuSection.SettingsUsers or
                TrayMenuSection.SettingsServers or
                TrayMenuSection.Devices or
                TrayMenuSection.Recovery or
                TrayMenuSection.Accounts;
            bool settingsOverviewActive = activeSection == TrayMenuSection.Settings;
            bool recoveryActive = activeSection == TrayMenuSection.Recovery;

            _settingsView.ApplyState(new SettingsViewState(settingsNavigationActive, settingsOverviewActive,
                recoveryActive, viewState.Description, _settingsAccessMessage));
            _quickPanelDetailDescriptionLabel.Visible = !settingsOverviewActive && !recoveryActive;

            if (!settingsNavigationActive)
            {
                return;
            }

            UpdateSettingsOverviewLinkState(TrayMenuSection.Settings);
            UpdateSettingsOverviewLinkState(TrayMenuSection.SettingsUsers);
            UpdateSettingsOverviewLinkState(TrayMenuSection.SettingsServers);
            UpdateSettingsOverviewLinkState(TrayMenuSection.Devices);
            UpdateSettingsOverviewLinkState(TrayMenuSection.Recovery);

            if (settingsOverviewActive || recoveryActive)
            {
                _quickPanelSelectionCard.Visible = false;
                _quickPanelMetricsCard.Visible = false;
                _quickPanelInsightCard.Visible = false;
            }

            if (activeSection is TrayMenuSection.SettingsUsers or TrayMenuSection.SettingsServers)
            {
                _quickPanelSelectionCard.Location = new Point(32, 205);
            }
            else if (activeSection == TrayMenuSection.Accounts)
            {
                _accountsView.LayoutContent(_quickPanelDetailPanel.Height);
            }
            else if (activeSection == TrayMenuSection.Devices)
            {
                _devicesView.LayoutContent(_quickPanelDetailPanel.Height);
            }

            LayoutSettingsOverview();
            _settingsView.BringToFront();
        }

        private void UpdateSettingsOverviewLinkState(TrayMenuSection section)
        {
            bool available = section == TrayMenuSection.Recovery
                ? !_authenticationService.IsAuthorized
                : !RequiresSettingsAuthentication(section) || _authenticationService.IsAuthorized;
            _settingsView.SetLinkState(section, available, _trayMenuController?.ActiveSection == section);
        }

        private void ApplyLoginProfileSelectionAvailability(TrayMenuViewState viewState)
        {
            if (viewState.SelectionLayout != TrayMenuSelectionLayout.LoginSelectors)
            {
                return;
            }

            bool profileSelectionLocked = _authenticationService.IsAuthorized ||
                _authenticationService.HasAuthorizedOtherProfile;
            _loginView.ProfileSelector.Enabled = !profileSelectionLocked && _loginView.ProfileSelector.Items.Count > 1;
            _loginView.ProfileSelector.Cursor = _loginView.ProfileSelector.Enabled ? Cursors.Hand : Cursors.Default;
        }

        private void UpdateAccountsPresentation()
        {
            bool active = _trayMenuController?.ActiveSection == TrayMenuSection.Accounts;
            _accountsView.Visible = active;
            _quickPanelDetailDescriptionLabel.Size = new Size(574, active ? 60 : 44);
            if (!active)
            {
                return;
            }
            _quickPanelSelectionCard.Visible = false;
            _quickPanelMetricsCard.Visible = false;
            _quickPanelInsightCard.Visible = false;
            _accountsView.LayoutContent(_quickPanelDetailPanel.Height);
            if (_registeredDeviceService.TryReadRegistration(out DesktopRegistration? registration) && registration != null)
            {
                _accountsView.Render(registration.UserKey, registration.Namespaces);
            }
            else
            {
                _accountsView.Render(string.Empty, Array.Empty<NamespaceItemDto>());
            }
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
            IReadOnlyList<CredentialEntryListItem> filteredCredentialEntries = GetFilteredCredentialEntries();
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
                CredentialGroupCount = _credentialGroups.Count,
                CredentialEntryCount = _credentialEntries.Count,
                FilteredCredentialEntryCount = filteredCredentialEntries.Count,
                SelectedCredentialGroupLabel = ResolveSelectedCredentialGroupLabel(),
                CredentialsLoading = _credentialsLoading,
                CredentialsErrorMessage = _credentialsErrorMessage,
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

            ApplyLoginProfileSelectionAvailability(viewState);

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

            UpdateServerRegistrationPresentation(viewState);
            UpdateAccountsPresentation();
            UpdateDevicesPresentation();
            UpdateFaqPresentation();
            UpdateSettingsOverviewPresentation(viewState);
            UpdateSettingsViewsPresentation();
        }

        private void UpdateSettingsViewsPresentation()
        {
            UpdateAccountRenamePresentation();
            _devicesView.Visible = _trayMenuController?.ActiveSection == TrayMenuSection.Devices;
            bool databaseActions = _trayMenuController?.ActiveSection == TrayMenuSection.SettingsServers;
            bool databaseRegistration = _trayMenuController?.ActiveSection == TrayMenuSection.Servers && _showInlineServerRegistration;
            _databaseView.ShowSection(databaseActions, databaseRegistration);
            if (databaseActions || databaseRegistration)
            {
                _quickPanelSelectionCard.Visible = false;
                _quickPanelInsightCard.Visible = false;
            }
            if (_trayMenuController?.ActiveSection == TrayMenuSection.SettingsUsers)
            {
                _quickPanelSelectionCard.Visible = false;
            }
        }

        private async Task LoadAccountDevicesAsync()
        {
            if (_trayMenuController?.ActiveSection != TrayMenuSection.Devices)
            {
                return;
            }

            _devicesLoading = true;
            _devicesErrorMessage = string.Empty;
            _devicePendingDeletionKey = string.Empty;
            UpdateDevicesPresentation();
            _quickPanelContainer.Refresh();
            await Task.Yield();

            try
            {
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

                _accountDeviceServerResults = await Task.WhenAll(requests).ConfigureAwait(true);
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
                _accountDeviceServerResults = Array.Empty<AccountDeviceServerResult>();
                _visibleAccountDevices = Array.Empty<DeviceListItem>();
                _devicesErrorMessage = exception.Message;
                _devicesLoadedOnce = true;
            }
            finally
            {
                _devicesLoading = false;
                UpdateDevicesPresentation();
            }
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
                    _credentialCollectionCacheKey = string.Empty;
                    _credentialCollectionLoaded = false;
                    _selectedCredentialGroupId = string.Empty;
                    _selectedCredentialEntryId = string.Empty;
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

        private async Task DeleteAccountFromQuickPanelAsync(string userId, NamespaceItemDto namespaceItem)
        {
            DialogResult confirmation = MessageBox.Show(
                "Removing this server subscription deregisters this device and removes server-side account data for this server. This cannot be undone.",
                "Remove server subscription?",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmation != DialogResult.OK)
            {
                UpdateQuickPanelState();
                return;
            }

            try
            {
                await _accountLifecycleService.DeleteAccountAsync(userId, namespaceItem);

                bool deletedLastAccountForActiveProfile =
                    string.Equals(userId, AppEnvironment.GetActiveUserId(), StringComparison.Ordinal) &&
                    (!_registeredDeviceService.TryReadRegistration(out DesktopRegistration? registration) ||
                     registration == null ||
                     registration.Namespaces.Count == 0);

                if (deletedLastAccountForActiveProfile)
                {
                    _trayMenuController?.SetActiveSection(TrayMenuSection.Login, triggerStateChanged: true);
                }

                RefreshQuickPanelSelections();
                UpdateQuickPanelState();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Server removal failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateServerRegistrationPresentation(TrayMenuViewState viewState)
        {
            bool loginSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.Login;
            _loginView.Visible = loginSectionActive;
            bool settingsUsersSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.SettingsUsers;
            bool settingsServersSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.SettingsServers;
            bool settingsActionSectionActive = settingsUsersSectionActive || settingsServersSectionActive;
            bool serversSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.Servers;
            bool credentialsSectionActive = viewState.SelectionLayout == TrayMenuSelectionLayout.CredentialSelectors;
            bool showInlineRegistration = serversSectionActive && _showInlineServerRegistration;
            bool isNewServerRegistration = _inlineServerRegistrationMode == InlineServerRegistrationMode.NewServer;
            _databaseView.GuidancePanel.Visible = showInlineRegistration;
            bool credentialGroupManagerActive = credentialsSectionActive &&
                _credentialWorkspaceMode is CredentialWorkspaceMode.ManageGroup or CredentialWorkspaceMode.CreateGroup;
            bool isAuthorizedForActiveNamespace = _authenticationService.IsAuthorizedForActiveNamespace;
            bool canStartRegistration = _authenticationService.IsAuthorized && !_authenticationInProgress;
            bool loginBlockedByAnotherProfile = _authenticationService.HasAuthorizedOtherProfile;
            bool canLoginToActiveNamespace = _authenticationService.CanLoginForActiveNamespace &&
                !_authenticationInProgress;
            bool canLogoutFromActiveNamespace = isAuthorizedForActiveNamespace && !_authenticationInProgress;

            bool showInlineUserRegistration = settingsUsersSectionActive && _showInlineUserRegistration;
            bool showInlineUserDeletion = settingsUsersSectionActive && _showInlineUserDeletion;
            if (showInlineUserDeletion)
            {
                _quickPanelDetailTitleLabel.Text = "Delete user profile";
                _quickPanelDetailDescriptionLabel.Text = string.Empty;
            }

            _quickPanelRegisterNewServerInlineButton.Visible = serversSectionActive && !showInlineRegistration;
            _quickPanelRegisterNewServerInlineButton.Enabled = canStartRegistration;
            _quickPanelServerRegistrationInlineButton.Visible = serversSectionActive;
            _quickPanelServerRegistrationInlineButton.Enabled = !_inlineServerRegistrationAwaitingCompletion && !_inlineServerRegistrationSubmitting && canStartRegistration;
            _quickPanelServerStatusValueLabel.Visible = serversSectionActive && !showInlineRegistration;
            _quickPanelServerLoginInlineButton.Visible = serversSectionActive && !showInlineRegistration;
            _quickPanelServerLoginInlineButton.Enabled = canLoginToActiveNamespace || canLogoutFromActiveNamespace;
            _quickPanelServerLoginInlineButton.Text = _authenticationInProgress
                ? isAuthorizedForActiveNamespace ? "Logging out..." : "Logging in..."
                : isAuthorizedForActiveNamespace ? "Logout" : "Login";
            if (loginSectionActive && loginBlockedByAnotherProfile)
            {
                _quickPanelDetailDescriptionLabel.Text = "Only one profile can be signed in at a time. Log out before signing in with another profile.";
            }
            _quickPanelSelectionCard.Visible = !credentialGroupManagerActive && !credentialsSectionActive;
            _quickPanelMetricsCard.Visible = !credentialsSectionActive && !loginSectionActive && !settingsActionSectionActive;
            _quickPanelMetricsCard.Location = new Point(32, 294);
            _quickPanelInsightCard.Location = new Point(32, 424);

            if (_authenticationInProgress)
            {
                _quickPanelServerLoginInlineButton.BackColor = Color.FromArgb(46, 48, 77);
                _quickPanelServerLoginInlineButton.ForeColor = Color.White;
                _quickPanelServerLoginInlineButton.FlatAppearance.BorderColor = Color.FromArgb(46, 48, 77);
            }
            else if (canLoginToActiveNamespace)
            {
                _quickPanelServerLoginInlineButton.BackColor = Color.FromArgb(120, 205, 0);
                _quickPanelServerLoginInlineButton.ForeColor = Color.FromArgb(24, 30, 24);
                _quickPanelServerLoginInlineButton.FlatAppearance.BorderColor = Color.FromArgb(120, 205, 0);
            }
            else if (canLogoutFromActiveNamespace)
            {
                _quickPanelServerLoginInlineButton.BackColor = Color.FromArgb(214, 88, 88);
                _quickPanelServerLoginInlineButton.ForeColor = Color.White;
                _quickPanelServerLoginInlineButton.FlatAppearance.BorderColor = Color.FromArgb(214, 88, 88);
            }
            else
            {
                _quickPanelServerLoginInlineButton.BackColor = Color.FromArgb(46, 48, 77);
                _quickPanelServerLoginInlineButton.ForeColor = Color.White;
                _quickPanelServerLoginInlineButton.FlatAppearance.BorderColor = Color.FromArgb(46, 48, 77);
            }

            if (credentialsSectionActive)
            {
                _quickPanelRegisterNewServerInlineButton.Visible = false;
                _quickPanelServerRegistrationInlineButton.Visible = false;
                _quickPanelServerStatusValueLabel.Visible = false;
                _quickPanelServerLoginInlineButton.Visible = false;
                _databaseView.SetServerRegistrationControlsVisible(false);
                return;
            }

            if (!serversSectionActive)
            {
                if (loginSectionActive)
                {
                    _quickPanelSelectionCard.Visible = false;
                    _loginView.ApplyState(new LoginViewState(
                        viewState.ShowUserSelector,
                        !_authenticationService.IsAuthorized && !loginBlockedByAnotherProfile && _loginView.ProfileSelector.Items.Count > 1,
                        isAuthorizedForActiveNamespace, _authenticationInProgress,
                        canLoginToActiveNamespace, canLogoutFromActiveNamespace));
                    _quickPanelInsightCard.Visible = false;
                    _databaseView.SetServerRegistrationControlsVisible(false);
                    return;
                }

                if (settingsActionSectionActive)
                {
                    if (settingsUsersSectionActive && (showInlineUserRegistration || showInlineUserDeletion))
                    {
                        _quickPanelSelectionCard.Visible = false;
                    }

                    _quickPanelInsightCard.Visible = false;
                    _databaseView.SetServerRegistrationControlsVisible(false);
                    return;
                }

                _quickPanelInsightCard.Visible = true;
                _quickPanelInsightCard.Size = new Size(578, 110);
                _quickPanelInsightTitleLabel.Location = new Point(16, 16);
                _quickPanelInsightTitleLabel.Text = viewState.InsightTitle;
                _quickPanelInsightBodyLabel.Text = viewState.InsightBody;
                _quickPanelInsightBodyLabel.Size = new Size(544, 56);
                _quickPanelInsightBodyLabel.Visible = true;
                _quickPanelRegisterNewServerInlineButton.Visible = false;
                _quickPanelServerRegistrationInlineButton.Visible = false;
                _quickPanelServerStatusValueLabel.Visible = false;
                _quickPanelServerLoginInlineButton.Visible = false;
                _databaseView.SetServerRegistrationControlsVisible(false);
                return;
            }

            if (!showInlineRegistration)
            {
                _quickPanelSelectionCard.Size = new Size(578, 108);
                _quickPanelInsightCard.Size = new Size(578, 110);
                _quickPanelInsightTitleLabel.Text = viewState.InsightTitle;
                _quickPanelInsightBodyLabel.Text = viewState.InsightBody;
                _quickPanelInsightBodyLabel.Size = new Size(544, 56);
                _quickPanelInsightBodyLabel.Visible = true;
                _quickPanelRegisterNewServerInlineButton.Text = "+ Add a server";
                _quickPanelRegisterNewServerInlineButton.Location = new Point(16, 14);
                _quickPanelRegisterNewServerInlineButton.Size = new Size(156, 28);
                _quickPanelServerRegistrationInlineButton.Text = "+ Sync existing server";
                _quickPanelServerRegistrationInlineButton.Location = new Point(180, 14);
                _quickPanelServerRegistrationInlineButton.Size = new Size(170, 28);
                _quickPanelServerStatusValueLabel.Visible = false;
                _quickPanelServerLoginInlineButton.Location = new Point(438, 14);
                _quickPanelServerLoginInlineButton.Size = new Size(122, 28);
                _databaseView.SetServerRegistrationControlsVisible(false);
                return;
            }

            _quickPanelSelectionCard.Visible = false;
            _quickPanelMetricsCard.Visible = false;
            _quickPanelDetailTitleLabel.Text = isNewServerRegistration ? "Add a server" : "Sync existing server";
            _quickPanelInsightCard.Visible = false;
            _quickPanelRegisterNewServerInlineButton.Visible = false;
            _quickPanelServerRegistrationInlineButton.Visible = false;
            _databaseView.ApplyRegistrationState(isNewServerRegistration,
                _inlineServerRegistrationSubmitting, _inlineServerRegistrationAwaitingCompletion,
                _serverRegistrationStatusMessage);
        }

        private void ShowServerAuthenticationProgress()
        {
            if (_trayMenuController?.ActiveSection != TrayMenuSection.Servers)
            {
                return;
            }

            bool isAuthorizedForActiveNamespace = _authenticationService.IsAuthorizedForActiveNamespace;
            _quickPanelServerLoginInlineButton.Enabled = true;
            _quickPanelServerLoginInlineButton.Text = isAuthorizedForActiveNamespace
                ? "Logging out..."
                : "Logging in...";
            _quickPanelServerLoginInlineButton.BackColor = Color.FromArgb(46, 48, 77);
            _quickPanelServerLoginInlineButton.ForeColor = Color.White;
            _quickPanelServerLoginInlineButton.FlatAppearance.BorderColor = Color.FromArgb(46, 48, 77);
        }

        private void ResetInlineServerRegistrationState(bool clearInputs)
        {
            _showInlineServerRegistration = false;
            _inlineServerRegistrationSubmitting = false;
            _inlineServerRegistrationAwaitingCompletion = false;
            _inlineServerRegistrationMode = InlineServerRegistrationMode.NamespaceSync;
            _serverRegistrationStatusMessage = string.Empty;
            _databaseView.QrPictureBox.Image = null;

            if (clearInputs)
            {
                _databaseView.UrlTextBox.Text = string.Empty;
                _databaseView.NameTextBox.Text = string.Empty;
            }
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
                    _ = EnsureCredentialCollectionLoadedAsync(forceRefresh: true);
                }
            }
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
                bool showGroupManager = _credentialWorkspaceMode is CredentialWorkspaceMode.ManageGroup or CredentialWorkspaceMode.CreateGroup;
                bool showCreateEntry = _credentialWorkspaceMode == CredentialWorkspaceMode.CreateEntry;
                bool showCreateGroup = _credentialWorkspaceMode == CredentialWorkspaceMode.CreateGroup;
                bool showEntryEditor = showCreateEntry ||
                    (!showGroupManager && !string.IsNullOrWhiteSpace(_selectedCredentialEntryId));
                bool canDeleteCredential = !showGroupManager && !showCreateEntry && GetSelectedCredentialEntry() != null;
                bool showCredentialReadEditDeleteMode = canDeleteCredential;
                bool showCredentialDeletionConfirmation = canDeleteCredential &&
                    IsDeletionConfirmationPending(CredentialDeletionConfirmation.Credential);
                bool canDeleteGroup = showGroupManager && !showCreateGroup && _credentialGroups.Any(group =>
                    string.Equals(group.GroupId, _selectedManagedGroupId, StringComparison.Ordinal));
                bool showGroupDeletionConfirmation = canDeleteGroup &&
                    IsDeletionConfirmationPending(CredentialDeletionConfirmation.Group);

                _quickPanelCredentialListPanel.UpdateEntries(
                    _credentialEntries,
                    _selectedCredentialEntryId,
                    _credentialsLoading,
                    _credentialsErrorMessage);

                _quickPanelDetailTitleLabel.Text = _credentialWorkspaceMode switch
                {
                    CredentialWorkspaceMode.ManageGroup => "Groups - Edit",
                    CredentialWorkspaceMode.CreateGroup => "Groups - Create",
                    CredentialWorkspaceMode.CreateEntry => "Credentials - Create",
                    _ => showCredentialReadEditDeleteMode
                        ? "Credentials - Read / Edit / Delete"
                        : "Credentials"
                };
                _quickPanelDetailDescriptionLabel.Text = _credentialWorkspaceMode switch
                {
                    CredentialWorkspaceMode.ManageGroup => "Select a credential group to edit its name.",
                    CredentialWorkspaceMode.CreateGroup => "Create a credential group for the active server.",
                    CredentialWorkspaceMode.CreateEntry => "Create a credential for the active server.",
                    _ => _quickPanelDetailDescriptionLabel.Text
                };
                ApplyCredentialSelectionCardMode(showGroupManager, showCreateGroup, showCreateEntry);
                _credentialWorkspace.ApplyEntryState(
                    showCreateEntry, canDeleteCredential, showCredentialDeletionConfirmation,
                    _credentialsLoading, _credentialEditorStatusMessage);
                _credentialWorkspace.ApplyGroupState(
                    showCreateGroup, canDeleteGroup, showGroupDeletionConfirmation,
                    _credentialsLoading, _groupEditorStatusMessage);
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
                    QueueSelectedCredentialEditorLoad();
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
                _credentialsLoading, _credentialWorkspace.GroupEditorPanel);
            _quickPanelSelectionCard.Visible = false;
        }

        private void PopulateCredentialAssignedGroupSelector()
        {
            string currentSelection = _credentialWorkspace.AssignedGroupComboBox.SelectedItem is SelectionOption option
                ? option.Value
                : string.Empty;

            _credentialWorkspace.AssignedGroupComboBox.Items.Clear();
            _credentialWorkspace.AssignedGroupComboBox.Items.Add(new SelectionOption("No group", string.Empty));

            foreach (CredentialGroupDto group in _credentialGroups)
            {
                _credentialWorkspace.AssignedGroupComboBox.Items.Add(new SelectionOption(group.GroupName, group.GroupId));
            }

            string preferredSelection = _credentialWorkspaceMode == CredentialWorkspaceMode.CreateEntry
                ? (!string.IsNullOrWhiteSpace(_selectedCredentialGroupId) ? _selectedCredentialGroupId : currentSelection)
                : currentSelection;

            SelectComboBoxValue(_credentialWorkspace.AssignedGroupComboBox, preferredSelection);
        }

        private void QueueSelectedCredentialEditorLoad()
        {
            CredentialEntryListItem? selectedEntry = GetSelectedCredentialEntry();
            PopulateCredentialAssignedGroupSelector();

            if (selectedEntry == null)
            {
                _credentialDetailLoadRequestVersion++;
                _credentialDetailLoading = false;
                ApplyCredentialDetailToEditor(null);
                return;
            }

            if (_credentialEntryDetails.TryGetValue(selectedEntry.Id, out CredentialEntryDetail? cachedDetail))
            {
                _credentialDetailLoading = false;
                ApplyCredentialDetailToEditor(cachedDetail);
                return;
            }

            int requestVersion = ++_credentialDetailLoadRequestVersion;
            _credentialDetailLoading = true;
            ApplyCredentialDetailLoadingState();
            _ = LoadSelectedCredentialIntoEditorAsync(selectedEntry, requestVersion);
        }

        private async Task LoadSelectedCredentialIntoEditorAsync(CredentialEntryListItem selectedEntry, int requestVersion)
        {
            try
            {
                CredentialEntryDetail detail = _credentialCollectionService.ReadEntryDetail(selectedEntry);

                if (requestVersion != _credentialDetailLoadRequestVersion ||
                    _credentialWorkspaceMode != CredentialWorkspaceMode.ViewEntry ||
                    !string.Equals(_selectedCredentialEntryId, selectedEntry.Id.ToString(), StringComparison.Ordinal))
                {
                    return;
                }

                _credentialEntryDetails[selectedEntry.Id] = detail;
                _credentialDetailLoading = false;
                ApplyCredentialDetailToEditor(detail);
            }
            catch (Exception ex)
            {
                if (requestVersion != _credentialDetailLoadRequestVersion)
                {
                    return;
                }

                _credentialDetailLoading = false;
                ApplyCredentialDetailToEditor(null);
                _credentialEditorStatusMessage = ex.Message;
                _credentialWorkspace.StatusLabel.Text = ex.Message;
            }
        }

        private void ApplyCredentialDetailLoadingState()
        {
            _updatingCredentialWorkspaceControls = true;

            try
            {
                _credentialWorkspace.ClearEntry();
                SetCredentialPasswordVisible(false);

                _credentialWorkspace.SetEntryAvailability(false, false, false);
            }
            finally
            {
                _updatingCredentialWorkspaceControls = false;
            }
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
                bool canSave = hasSelection && !_credentialsLoading && !_credentialDetailLoading;
                _credentialWorkspace.SetEntryAvailability(hasSelection, canSave, canSave);

                if (string.IsNullOrWhiteSpace(_credentialEditorStatusMessage))
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
            SelectComboBoxValue(_credentialWorkspace.AssignedGroupComboBox, _selectedCredentialGroupId);

            _credentialWorkspace.SetEntryAvailability(true, !_credentialsLoading, true);

            if (string.IsNullOrWhiteSpace(_credentialEditorStatusMessage))
            {
                _credentialWorkspace.StatusLabel.Text = "Create a new credential for the active server. The current group filter becomes the default group assignment.";
            }
        }

        private void PopulateManagedGroupEditor()
        {
            _credentialWorkspace.ManagedGroupComboBox.Items.Clear();
            foreach (CredentialGroupDto group in _credentialGroups)
            {
                _credentialWorkspace.ManagedGroupComboBox.Items.Add(new SelectionOption(group.GroupName, group.GroupId));
            }

            if (string.IsNullOrWhiteSpace(_selectedManagedGroupId))
            {
                _selectedManagedGroupId = _credentialGroups.FirstOrDefault()?.GroupId ?? string.Empty;
            }

            SelectComboBoxValue(_credentialWorkspace.ManagedGroupComboBox, _selectedManagedGroupId);

            CredentialGroupDto? selectedGroup = _credentialGroups.FirstOrDefault(group =>
                string.Equals(group.GroupId, _selectedManagedGroupId, StringComparison.Ordinal));

            _credentialWorkspace.ManagedGroupNameTextBox.Text = selectedGroup?.GroupName ?? string.Empty;
            _credentialWorkspace.ManagedGroupNameTextBox.Enabled = selectedGroup != null && !_credentialsLoading;
            _credentialWorkspace.GroupSaveButton.Enabled = !_credentialsLoading && (_credentialWorkspaceMode == CredentialWorkspaceMode.CreateGroup || selectedGroup != null);
            _credentialWorkspace.GroupDeleteButton.Enabled = !_credentialsLoading && _credentialWorkspaceMode == CredentialWorkspaceMode.ManageGroup && selectedGroup != null;
            _credentialWorkspace.GroupCreateButton.Enabled = !_credentialsLoading;

            if (_credentialWorkspaceMode == CredentialWorkspaceMode.CreateGroup)
            {
                _credentialWorkspace.NewGroupNameTextBox.Enabled = !_credentialsLoading;
            }
            else
            {
                _credentialWorkspace.NewGroupNameTextBox.Text = string.Empty;
                _credentialWorkspace.NewGroupNameTextBox.Enabled = false;
            }

            if (string.IsNullOrWhiteSpace(_groupEditorStatusMessage))
            {
                if (_credentialGroups.Count == 0 && _credentialWorkspaceMode != CredentialWorkspaceMode.CreateGroup) {
                    _credentialWorkspace.GroupStatusLabel.Text = "No groups returned for the active server.";
                }
            }
        }

        private CredentialEntryListItem? GetSelectedCredentialEntry()
        {
            return _credentialEntries.FirstOrDefault(entry => string.Equals(entry.Id.ToString(), _selectedCredentialEntryId, StringComparison.Ordinal));
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

        private void ClearCredentialEditorStatus()
        {
            _credentialEditorStatusMessage = string.Empty;
            _credentialWorkspace.StatusLabel.Text = string.Empty;
        }

        private void SetCredentialPasswordVisible(bool visible)
        {
            _credentialWorkspace.PasswordTextBox.UseSystemPasswordChar = !visible;
        }

        private void SetCredentialEditorStatus(string message)
        {
            _credentialEditorStatusMessage = message;
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
            _groupEditorStatusMessage = string.Empty;
            _credentialWorkspace.GroupStatusLabel.Text = string.Empty;
        }

        private void quickPanelUserComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            TrayDropdown selector = sender as TrayDropdown ?? _credentialSelection.GroupSelector;
            if (_updatingQuickPanelSelections || selector.SelectedItem is not SelectionOption option)
            {
                return;
            }

            if (_trayMenuController?.ActiveSection == TrayMenuSection.Credentials)
            {
                _selectedCredentialGroupId = option.Value;
                _selectedCredentialEntryId = string.Empty;
                PopulateCredentialSelectors();
                RefreshCredentialQuickPanelState();
                return;
            }

            AppEnvironment.SetActiveUser(option.Value);
            _credentialCollectionCacheKey = string.Empty;
            _credentialCollectionLoaded = false;
            RefreshQuickPanelSelections();
            ResetDevicesState();
            UpdateQuickPanelState();

            if (_trayMenuController?.ActiveSection == TrayMenuSection.Devices)
            {
                _ = LoadAccountDevicesAsync();
            }
        }

        private void quickPanelNamespaceComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            TrayDropdown selector = sender as TrayDropdown ?? _credentialSelection.EntrySelector;
            if (_updatingQuickPanelSelections || selector.SelectedItem is not SelectionOption option)
            {
                return;
            }

            if (_trayMenuController?.ActiveSection == TrayMenuSection.Credentials)
            {
                _selectedCredentialEntryId = option.Value;
                _credentialWorkspaceMode = CredentialWorkspaceMode.ViewEntry;
                RefreshCredentialQuickPanelState();
                return;
            }

            AppEnvironment.SetActiveNamespace(option.Value);
            _credentialCollectionCacheKey = string.Empty;
            _credentialCollectionLoaded = false;
            ResetDevicesState();
            UpdateQuickPanelState();

            if (_trayMenuController?.ActiveSection == TrayMenuSection.Devices)
            {
                _ = LoadAccountDevicesAsync();
            }
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
                    _credentialCollectionCacheKey = string.Empty;
                    _credentialCollectionLoaded = false;
                    _selectedCredentialGroupId = string.Empty;
                    _selectedCredentialEntryId = string.Empty;
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

        private void PopulateSettingsServerActions()
        {
            bool canManageServers = _registeredDeviceService.TryReadRegistration(out DesktopRegistration? registration) &&
                registration is { Namespaces.Count: > 0 };

            int expectedItems = canManageServers ? 3 : 2;
            if (_databaseView.ActionComboBox.Items.Count == expectedItems)
            {
                return;
            }

            _databaseView.ActionComboBox.Items.Clear();
            _databaseView.ActionComboBox.Items.Add(new SelectionOption("Select database action", string.Empty));
            _databaseView.ActionComboBox.Items.Add(new SelectionOption("Grant access to an existing database", "sync"));

            if (canManageServers)
            {
                _databaseView.ActionComboBox.Items.Add(new SelectionOption("Manage subscription...", "manage"));
            }

            _databaseView.ActionComboBox.SetSelectedIndexSilently(0);
        }

        private void quickPanelSettingsServerActionComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_databaseView.ActionComboBox.SelectedItem is not SelectionOption option ||
                _trayMenuController?.ActiveSection != TrayMenuSection.SettingsServers)
            {
                return;
            }

            _databaseView.ActionComboBox.SetSelectedIndexSilently(0);

            if (option.Value == "register")
            {
                _trayMenuController.SetActiveSection(TrayMenuSection.Servers, triggerStateChanged: false);
                ShowInlineServerRegistration(InlineServerRegistrationMode.NewServer);
                return;
            }

            if (option.Value == "sync")
            {
                _trayMenuController.SetActiveSection(TrayMenuSection.Servers, triggerStateChanged: false);
                ShowInlineServerRegistration(InlineServerRegistrationMode.NamespaceSync);
                return;
            }

            if (option.Value == "manage")
            {
                _trayMenuController.SetActiveSection(TrayMenuSection.Accounts, triggerStateChanged: true);
            }
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
            if (_credentialWorkspaceMode is CredentialWorkspaceMode.ManageGroup or CredentialWorkspaceMode.CreateGroup)
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
            SetCredentialWorkspaceMode(_credentialWorkspaceMode == CredentialWorkspaceMode.CreateGroup
                ? CredentialWorkspaceMode.ManageGroup
                : CredentialWorkspaceMode.ViewEntry);
        }

        private async void quickPanelCredentialSaveButton_Click(object? sender, EventArgs e)
        {
            if (IsDeletionConfirmationPending(CredentialDeletionConfirmation.Credential))
            {
                _deletionConfirmation = CredentialDeletionConfirmation.None;
                ClearCredentialEditorStatus();
                RenderCredentialWorkspace();
                return;
            }

            string label = _credentialWorkspace.LabelTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                _credentialEditorStatusMessage = "Please enter a credential label.";
                RenderCredentialWorkspace();
                return;
            }

            string assignedGroupId = GetSelectedAssignedCredentialGroupId();
            BrowserRegistryDto registry = BuildCredentialRegistryFromEditor();

            try
            {
                _credentialsLoading = true;
                _credentialEditorStatusMessage = string.Empty;
                RenderCredentialWorkspace();

                if (_credentialWorkspaceMode == CredentialWorkspaceMode.CreateEntry)
                {
                    CredentialEntryListItem? createdEntry = await _credentialCollectionService
                        .CreateEntryAsync(new CredentialEntryDraft(null, label, registry, assignedGroupId))
                        .ConfigureAwait(true);

                    _credentialWorkspaceMode = CredentialWorkspaceMode.ViewEntry;
                    _credentialEditorStatusMessage = "Credential created.";
                    await ReloadCredentialWorkspaceAsync(createdEntry?.Id.ToString()).ConfigureAwait(true);
                    return;
                }

                CredentialEntryListItem? selectedEntry = GetSelectedCredentialEntry();
                if (selectedEntry == null)
                {
                    _credentialEditorStatusMessage = "Please select a credential entry first.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(assignedGroupId) && !string.IsNullOrWhiteSpace(selectedEntry.GroupId))
                {
                    _credentialEditorStatusMessage = "Removing a group assignment is not supported by the current tray contract.";
                    return;
                }

                CredentialEntryListItem? updatedEntry = await _credentialCollectionService
                    .UpdateEntryAsync(new CredentialEntryDraft(selectedEntry.Id, label, registry, assignedGroupId))
                    .ConfigureAwait(true);

                _credentialEditorStatusMessage = "Credential updated.";
                await ReloadCredentialWorkspaceAsync(updatedEntry?.Id.ToString() ?? selectedEntry.Id.ToString()).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _credentialEditorStatusMessage = ex.Message;
            }
            finally
            {
                _credentialsLoading = false;
                RenderCredentialWorkspace();
                UpdateQuickPanelState();
            }
        }

        private async void quickPanelCredentialDeleteButton_Click(object? sender, EventArgs e)
        {
            CredentialEntryListItem? selectedEntry = GetSelectedCredentialEntry();
            if (selectedEntry == null)
            {
                _credentialEditorStatusMessage = "Please select a credential entry first.";
                RenderCredentialWorkspace();
                return;
            }

            if (!IsDeletionConfirmationPending(CredentialDeletionConfirmation.Credential))
            {
                _deletionConfirmation = CredentialDeletionConfirmation.Credential;
                ClearCredentialEditorStatus();
                RenderCredentialWorkspace();
                return;
            }

            try
            {
                _credentialsLoading = true;
                ClearCredentialEditorStatus();
                RenderCredentialWorkspace();

                await _credentialCollectionService.DeleteEntryAsync(selectedEntry.Id).ConfigureAwait(true);

                _deletionConfirmation = CredentialDeletionConfirmation.None;
                _selectedCredentialEntryId = string.Empty;
                _credentialSelectorsFingerprint = string.Empty;
                _credentialEditorStatusMessage = "Credential deleted.";
                await ReloadCredentialWorkspaceAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _deletionConfirmation = CredentialDeletionConfirmation.None;
                _credentialEditorStatusMessage = ex.Message;
            }
            finally
            {
                _credentialsLoading = false;
                RenderCredentialWorkspace();
                UpdateQuickPanelState();
            }
        }

        private async void quickPanelCredentialGroupSaveButton_Click(object? sender, EventArgs e)
        {
            if (IsDeletionConfirmationPending(CredentialDeletionConfirmation.Group))
            {
                _deletionConfirmation = CredentialDeletionConfirmation.None;
                ClearGroupEditorStatus();
                RenderCredentialWorkspace();
                return;
            }

            string editedGroupName = _credentialWorkspace.ManagedGroupNameTextBox.Text.Trim();

            try
            {
                _credentialsLoading = true;
                ClearGroupEditorStatus();
                RenderCredentialWorkspace();

                if (_credentialWorkspaceMode == CredentialWorkspaceMode.CreateGroup)
                {
                    string newGroupName = _credentialWorkspace.NewGroupNameTextBox.Text.Trim();
                    if (string.IsNullOrWhiteSpace(newGroupName))
                    {
                        _groupEditorStatusMessage = "Please enter a group name.";
                        return;
                    }

                    CredentialGroupDto? createdGroup = await _credentialCollectionService
                        .CreateGroupAsync(newGroupName)
                        .ConfigureAwait(true);

                    _credentialWorkspaceMode = CredentialWorkspaceMode.ViewEntry;
                    _groupEditorStatusMessage = "Group created.";
                    _selectedManagedGroupId = createdGroup?.GroupId ?? _selectedManagedGroupId;
                    _credentialSelectorsFingerprint = string.Empty;
                    await ReloadCredentialWorkspaceAsync().ConfigureAwait(true);
                    RefreshCredentialQuickPanelState();
                    return;
                }

                if (string.IsNullOrWhiteSpace(_selectedManagedGroupId))
                {
                    _groupEditorStatusMessage = "Please select a group first.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(editedGroupName))
                {
                    _groupEditorStatusMessage = "Please enter a group name.";
                    return;
                }

                await _credentialCollectionService
                    .UpdateGroupAsync(_selectedManagedGroupId, editedGroupName)
                    .ConfigureAwait(true);

                _credentialWorkspaceMode = CredentialWorkspaceMode.ViewEntry;
                _credentialSelectorsFingerprint = string.Empty;
                _groupEditorStatusMessage = "Group updated.";
                await ReloadCredentialWorkspaceAsync().ConfigureAwait(true);
                RefreshCredentialQuickPanelState();
            }
            catch (Exception ex)
            {
                _groupEditorStatusMessage = ex.Message;
            }
            finally
            {
                _credentialsLoading = false;
                RenderCredentialWorkspace();
                UpdateQuickPanelState();
            }
        }

        private async void quickPanelCredentialGroupDeleteButton_Click(object? sender, EventArgs e)
        {
            CredentialGroupDto? selectedGroup = _credentialGroups.FirstOrDefault(group =>
                string.Equals(group.GroupId, _selectedManagedGroupId, StringComparison.Ordinal));
            if (selectedGroup == null)
            {
                _groupEditorStatusMessage = "Please select a group first.";
                RenderCredentialWorkspace();
                return;
            }

            if (!IsDeletionConfirmationPending(CredentialDeletionConfirmation.Group))
            {
                _deletionConfirmation = CredentialDeletionConfirmation.Group;
                ClearGroupEditorStatus();
                RenderCredentialWorkspace();
                return;
            }

            try
            {
                _credentialsLoading = true;
                ClearGroupEditorStatus();
                RenderCredentialWorkspace();

                await _credentialCollectionService
                    .DeleteGroupAsync(selectedGroup.GroupId)
                    .ConfigureAwait(true);

                _deletionConfirmation = CredentialDeletionConfirmation.None;
                if (string.Equals(_selectedCredentialGroupId, selectedGroup.GroupId, StringComparison.Ordinal))
                {
                    _selectedCredentialGroupId = string.Empty;
                }

                _selectedManagedGroupId = string.Empty;
                _credentialSelectorsFingerprint = string.Empty;
                _groupEditorStatusMessage = "Group deleted.";
                await ReloadCredentialWorkspaceAsync().ConfigureAwait(true);
                RefreshCredentialQuickPanelState();
            }
            catch (Exception ex)
            {
                _deletionConfirmation = CredentialDeletionConfirmation.None;
                _groupEditorStatusMessage = ex.Message;
            }
            finally
            {
                _credentialsLoading = false;
                RenderCredentialWorkspace();
                UpdateQuickPanelState();
            }
        }

        private void quickPanelCredentialGroupCreateButton_Click(object? sender, EventArgs e)
        {
            EnterCredentialGroupManager(showCreateGroup: true);
        }

        private void EnterCredentialCreateMode()
        {
            ClearCredentialEditorStatus();
            _credentialWorkspaceMode = CredentialWorkspaceMode.CreateEntry;
            ResetCredentialCreateEditor();
            RenderCredentialWorkspace();
        }

        private void EnterCredentialGroupManager(bool showCreateGroup)
        {
            if (!showCreateGroup && !string.IsNullOrWhiteSpace(_selectedCredentialGroupId))
            {
                _selectedManagedGroupId = _selectedCredentialGroupId;
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
            _credentialWorkspaceMode = mode;
            RenderCredentialWorkspace();
        }

        private void quickPanelManagedGroupComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_updatingCredentialWorkspaceControls || _credentialWorkspace.ManagedGroupComboBox.SelectedItem is not SelectionOption option)
            {
                return;
            }

            _selectedManagedGroupId = option.Value;
            ClearGroupEditorStatus();
            RenderCredentialWorkspace();
        }

        private async Task ReloadCredentialWorkspaceAsync(string? selectedEntryId = null)
        {
            _credentialCollectionCacheKey = string.Empty;
            _credentialCollectionLoaded = false;
            _credentialEntryDetails.Clear();
            _credentialDetailLoading = false;
            _credentialDetailLoadRequestVersion++;

            if (!string.IsNullOrWhiteSpace(selectedEntryId))
            {
                _selectedCredentialEntryId = selectedEntryId;
            }

            await EnsureCredentialCollectionLoadedAsync(forceRefresh: true).ConfigureAwait(true);
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
