using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
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
        private const int TextBoxHorizontalPadding = 6;
        private const int EmSetMargins = 0x00D3;
        private const int EcLeftMargin = 0x0001;
        private const int EcRightMargin = 0x0002;
        private const int QuickPanelGuidanceLeft = 32;
        private const int QuickPanelGuidanceTop = 190;
        private const int QuickPanelGuidanceListTop = 384;
        private const int QuickPanelContentBottomMargin = 15;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly ToolStripMenuItem _userSelectionToolStripMenuItem = new("user selection");
        private readonly ToolStripMenuItem _namespaceSelectionToolStripMenuItem = new("server selection");
        private readonly ToolStripMenuItem _newUserToolStripMenuItem = new("new user");
        private readonly ToolStripMenuItem _openTrayPanelToolStripMenuItem = new("open tray panel");
        private readonly UserSelectionMenuController _userSelectionMenuController;
        private readonly NamespaceSelectionMenuController _namespaceSelectionMenuController;
        private readonly Panel _quickPanelContainer = new BufferedPanel();
        private readonly Panel _quickPanelMenuPanel = new();
        private readonly Panel _quickPanelDividerPanel = new();
        private readonly Panel _quickPanelDetailPanel = new();
        private readonly Panel _quickPanelResizeGrip = new();
        private readonly Label _quickPanelEyebrowLabel = new();
        private readonly Label _quickPanelTitleLabel = new();
        private readonly Label _quickPanelProfileLabel = new();
        private readonly Label _quickPanelStatusLabel = new();
        private readonly Label _quickPanelSummaryLabel = new();
        private readonly FlowLayoutPanel _quickPanelServerStatusPanel = new();
        private readonly Label _quickPanelMenuSectionLabel = new();
        private readonly FlowLayoutPanel _quickPanelNavigationPanel = new();
        private readonly Button _quickPanelLoginNavButton = new();
        private readonly FlowLayoutPanel _quickPanelAccountsPanel = new();
        private readonly FlowLayoutPanel _quickPanelDevicesPanel = new();
        private readonly Label _quickPanelDevicesSourceLabel = new();
        private readonly TrayDropdown _quickPanelDevicesSourceComboBox = new();
        private readonly Panel _quickPanelAccountsGuidancePanel = new();
        private readonly RichTextBox _quickPanelAccountsGuidanceBox = new();
        private readonly Panel _quickPanelFaqPanel = new();
        private readonly Panel _quickPanelDevicesGuidancePanel = new();
        private readonly RichTextBox _quickPanelDevicesGuidanceBox = new();
        private readonly Button _quickPanelCredentialsNavButton = new();
        private readonly Button _quickPanelSettingsNavButton = new();
        private readonly Button _quickPanelFaqNavButton = new();
        private readonly Panel _quickPanelSettingsOverviewPanel = new();
        private readonly FlowLayoutPanel _quickPanelSettingsNavigationPanel = new();
        private readonly RichTextBox _quickPanelSettingsDescriptionBox = new();
        private readonly Label _quickPanelSettingsOverviewLink = new();
        private readonly Label _quickPanelSettingsProfileLink = new();
        private readonly Label _quickPanelSettingsServersLink = new();
        private readonly Label _quickPanelSettingsDevicesLink = new();
        private readonly Label _quickPanelSettingsAccountLink = new();
        private string _settingsAccessMessage = string.Empty;
        private readonly Label _quickPanelActionSectionLabel = new();
        private readonly Label _quickPanelDetailEyebrowLabel = new();
        private readonly Label _quickPanelDetailTitleLabel = new();
        private readonly Label _quickPanelDetailDescriptionLabel = new();
        private readonly Font _quickPanelDetailDefaultTitleFont = new("Segoe UI", 24F, FontStyle.Bold);
        private readonly Font _quickPanelDevicesTitleFont = new("Segoe UI", 15F, FontStyle.Bold);
        private readonly Font _quickPanelDevicesGuidanceFont = new("Segoe UI", 8.5F);
        private readonly Font _quickPanelDevicesGuidanceBoldFont = new("Segoe UI", 8.5F, FontStyle.Bold);
        private readonly Panel _quickPanelSelectionCard = new();
        private readonly Label _quickPanelUserLabel = new();
        private readonly TrayDropdown _quickPanelUserComboBox = new();
        private readonly Label _quickPanelNamespaceLabel = new();
        private readonly TrayDropdown _quickPanelNamespaceComboBox = new();
        private readonly Label _quickPanelSettingsUserActionLabel = new();
        private readonly TrayDropdown _quickPanelSettingsUserActionComboBox = new();
        private readonly Label _quickPanelSettingsServerActionLabel = new();
        private readonly TrayDropdown _quickPanelSettingsServerActionComboBox = new();
        private readonly Button _quickPanelCredentialGroupSettingsButton = new();
        private readonly Button _quickPanelCredentialEntryCreateIconButton = new();
        private readonly Label _quickPanelHomeActiveUserLabel = new();
        private readonly Label _quickPanelHomeActiveServerLabel = new();
        private readonly Label _quickPanelHomeSessionLabel = new();
        private readonly Panel _quickPanelMetricsCard = new();
        private readonly Panel _quickPanelInsightCard = new();
        private readonly Label _quickPanelInsightTitleLabel = new();
        private readonly Label _quickPanelInsightBodyLabel = new();
        private readonly Panel _quickPanelServerRegistrationGuidancePanel = new();
        private readonly RichTextBox _quickPanelServerRegistrationGuidanceBox = new();
        private readonly Button _quickPanelRegisterNewServerInlineButton = new();
        private readonly Button _quickPanelServerRegistrationInlineButton = new();
        private readonly Label _quickPanelServerStatusValueLabel = new();
        private readonly Button _quickPanelServerLoginInlineButton = new();
        private readonly Label _quickPanelServerRegistrationUrlLabel = new();
        private readonly TextBox _quickPanelServerRegistrationUrlTextBox = new();
        private readonly Label _quickPanelServerRegistrationNameLabel = new();
        private readonly TextBox _quickPanelServerRegistrationNameTextBox = new();
        private readonly Button _quickPanelServerRegistrationSubmitButton = new();
        private readonly Button _quickPanelServerRegistrationBackButton = new();
        private readonly Label _quickPanelServerRegistrationStatusLabel = new();
        private readonly PictureBox _quickPanelServerRegistrationQrPictureBox = new();
        private readonly Panel _quickPanelUserRegistrationPanel = new();
        private readonly Label _quickPanelUserRegistrationUrlLabel = new();
        private readonly TextBox _quickPanelUserRegistrationUrlTextBox = new();
        private readonly Label _quickPanelUserRegistrationNameLabel = new();
        private readonly TextBox _quickPanelUserRegistrationNameTextBox = new();
        private readonly Button _quickPanelUserRegistrationSubmitButton = new();
        private readonly Button _quickPanelUserRegistrationBackButton = new();
        private readonly Label _quickPanelUserRegistrationStatusLabel = new();
        private readonly PictureBox _quickPanelUserRegistrationQrPictureBox = new();
        private readonly Panel _quickPanelUserDeletionPanel = new();
        private readonly Label _quickPanelUserDeletionMessageLabel = new();
        private readonly Label _quickPanelUserDeletionProfileLabel = new();
        private readonly Button _quickPanelUserDeletionCancelButton = new();
        private readonly Button _quickPanelUserDeletionConfirmButton = new();
        private readonly Label _quickPanelAccountValueLabel = new();
        private readonly Label _quickPanelDeviceValueLabel = new();
        private readonly Label _quickPanelConnectionValueLabel = new();
        private readonly Label _quickPanelLastUpdatedValueLabel = new();
        private readonly FlowLayoutPanel _quickPanelActionPanel = new();
        private readonly Button _quickPanelPrimaryActionButton = new();
        private readonly Button _quickPanelRefreshButton = new();
        private readonly Button _quickPanelHideButton = new();
        private readonly Button _quickPanelExitButton = new();
        private readonly ToolTip _quickPanelToolTip = new();
        private readonly Panel _quickPanelCredentialWorkspacePanel = new();
        private readonly Panel _quickPanelCredentialEntryEditorPanel = new();
        private readonly Panel _quickPanelCredentialGroupEditorPanel = new();
        private readonly Button _quickPanelCredentialBackButton = new();
        private readonly Button _quickPanelCredentialDeleteButton = new();
        private readonly Button _quickPanelCredentialSaveButton = new();
        private readonly Button _quickPanelCredentialGroupSaveButton = new();
        private readonly Button _quickPanelCredentialGroupDeleteButton = new();
        private readonly Button _quickPanelCredentialGroupCreateButton = new();
        private readonly Label _quickPanelCredentialStatusLabel = new();
        private readonly Label _quickPanelGroupStatusLabel = new();
        private readonly Panel _quickPanelGroupDeletionInfoPanel = new();
        private readonly Label _quickPanelGroupDeletionInfoLabel = new();
        private readonly Label _quickPanelCredentialEditorModeLabel = new();
        private readonly Label _quickPanelCredentialLabelEditorLabel = new();
        private readonly TextBox _quickPanelCredentialLabelTextBox = new();
        private readonly Label _quickPanelCredentialAssignedGroupLabel = new();
        private readonly TrayDropdown _quickPanelCredentialAssignedGroupComboBox = new();
        private readonly Label _quickPanelCredentialDomainLabel = new();
        private readonly TextBox _quickPanelCredentialDomainTextBox = new();
        private readonly Label _quickPanelCredentialUsernameLabel = new();
        private readonly TextBox _quickPanelCredentialUsernameTextBox = new();
        private readonly Button _quickPanelCredentialUsernameCopyButton = new();
        private readonly Label _quickPanelCredentialPasswordLabel = new();
        private readonly TextBox _quickPanelCredentialPasswordTextBox = new();
        private readonly Button _quickPanelCredentialPasswordGenerateButton = new();
        private readonly Button _quickPanelCredentialPasswordRevealButton = new();
        private readonly Button _quickPanelCredentialPasswordCopyButton = new();
        private readonly Label _quickPanelCredentialDescriptionLabel = new();
        private readonly TextBox _quickPanelCredentialDescriptionTextBox = new();
        private readonly Button _quickPanelCredentialDescriptionCopyButton = new();
        private readonly Label _quickPanelManagedGroupLabel = new();
        private readonly TrayDropdown _quickPanelManagedGroupComboBox = new();
        private readonly Label _quickPanelManagedGroupNameLabel = new();
        private readonly TextBox _quickPanelManagedGroupNameTextBox = new();
        private readonly Label _quickPanelNewGroupNameLabel = new();
        private readonly TextBox _quickPanelNewGroupNameTextBox = new();
        private bool _updatingQuickPanelSelections;
        private bool _quickPanelRefreshInProgress;
        private string? _lastTrayMessage;
        private ToolTipIcon _lastTrayMessageIcon = ToolTipIcon.None;
        private DateTimeOffset _lastTrayStateUpdatedAt = DateTimeOffset.Now;
        private readonly TrayMenuViewRegistry _trayMenuViews = new();
        private readonly CredentialCollectionService _credentialCollectionService;
        private TrayMenuController? _trayMenuController;
        private TrayPanelHost? _quickPanelHost;
        private bool _quickPanelResizing;
        private int _quickPanelResizeStartHeight;
        private int _quickPanelResizeStartScreenY;
        private int _quickPanelHeight = 660;
        private bool _showExpandedTrayMenu;
        private bool _reopenExpandedTrayMenuRequested;
        private bool _explicitQuickPanelCloseRequested;
        private Point _lastTrayMenuScreenPoint;
        private bool _quickPanelPositionPinned;
        private bool _quickPanelDragging;
        private Point _quickPanelDragOffset;
        private bool _credentialsLoading;
        private string _credentialsErrorMessage = string.Empty;
        private string _selectedCredentialGroupId = string.Empty;
        private string _selectedCredentialEntryId = string.Empty;
        private string _credentialCollectionCacheKey = string.Empty;
        private bool _credentialCollectionLoaded;
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
        private IReadOnlyList<AccountDeviceListItem> _visibleAccountDevices = Array.Empty<AccountDeviceListItem>();
        private CredentialDeletionConfirmation _deletionConfirmation;
        private string _credentialEditorStatusMessage = string.Empty;
        private string _groupEditorStatusMessage = string.Empty;
        private string _serverRegistrationStatusMessage = string.Empty;
        private string _selectedManagedGroupId = string.Empty;
        private IReadOnlyList<CredentialGroupDto> _credentialGroups = Array.Empty<CredentialGroupDto>();
        private IReadOnlyList<CredentialEntryListItem> _credentialEntries = Array.Empty<CredentialEntryListItem>();
        private readonly Dictionary<int, CredentialEntryDetail> _credentialEntryDetails = new();
        private IReadOnlyList<TrayServerStatus> _renderedServerStatuses = Array.Empty<TrayServerStatus>();
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

        private readonly record struct TrayServerStatus(string NamespaceId, string Name, bool IsConnected, bool IsActive);

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

        private sealed class AccountDeviceListItem
        {
            public AccountDeviceDto Device { get; init; } = new();

            public NamespaceItemDto NamespaceItem { get; init; } = new();

            public string SourceUrl { get; init; } = string.Empty;

            public string IdentityKey { get; init; } = string.Empty;
        }

        private void InitializeTrayShell()
        {
            contextMenuStrip1.AutoSize = true;
            contextMenuStrip1.MinimumSize = new Size(240, 0);
            contextMenuStrip1.Padding = Padding.Empty;
            contextMenuStrip1.BackColor = Color.FromArgb(19, 33, 50);
            contextMenuStrip1.ForeColor = Color.White;
            contextMenuStrip1.ShowImageMargin = false;
            contextMenuStrip1.RenderMode = ToolStripRenderMode.Professional;
            contextMenuStrip1.Renderer = new ToolStripProfessionalRenderer(new TrayMenuColorTable());

            foreach (ToolStripItem menuItem in contextMenuStrip1.Items)
            {
                if (menuItem is ToolStripMenuItem toolStripMenuItem)
                {
                    toolStripMenuItem.ForeColor = Color.White;
                    toolStripMenuItem.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
                    toolStripMenuItem.Padding = new Padding(8, 6, 8, 6);
                }
            }

            notifyIcon1.MouseUp += notifyIcon1_MouseUp;
            notifyIcon1.DoubleClick += notifyIcon1_DoubleClick;

            BuildQuickPanel();
            InitializeQuickPanelDropDown();
        }

        private void InitializeQuickPanelDropDown()
        {
            _quickPanelHost = new TrayPanelHost(_quickPanelContainer)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = _quickPanelContainer.Size,
                Visible = false
            };

            contextMenuStrip1.Items.Add(_quickPanelHost);
        }

        private void BuildQuickPanel()
        {
            SuspendLayout();

            _quickPanelContainer.Padding = new Padding(0);
            _quickPanelContainer.Margin = Padding.Empty;
            _quickPanelContainer.Location = Point.Empty;
            _quickPanelContainer.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelContainer.Visible = false;
            _quickPanelContainer.Size = new Size(820, _quickPanelHeight);
            _quickPanelContainer.MouseDown += quickPanelContainer_MouseDown;
            _quickPanelContainer.MouseMove += quickPanelContainer_MouseMove;
            _quickPanelContainer.MouseUp += quickPanelContainer_MouseUp;

            _quickPanelMenuPanel.BackColor = Color.FromArgb(36, 38, 63);
            _quickPanelMenuPanel.Location = new Point(0, 0);
            _quickPanelMenuPanel.Size = new Size(176, _quickPanelHeight);

            _quickPanelDividerPanel.BackColor = Color.FromArgb(59, 61, 88);
            _quickPanelDividerPanel.Location = new Point(176, 0);
            _quickPanelDividerPanel.Size = new Size(1, _quickPanelHeight);

            _quickPanelDetailPanel.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelDetailPanel.Location = new Point(177, 0);
            _quickPanelDetailPanel.Size = new Size(643, _quickPanelHeight);

            _quickPanelEyebrowLabel.AutoSize = true;
            _quickPanelEyebrowLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelEyebrowLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelEyebrowLabel.Location = new Point(18, 16);
            _quickPanelEyebrowLabel.Text = "MENU";
            ConfigureQuickPanelDragHandle(_quickPanelEyebrowLabel);

            _quickPanelTitleLabel.AutoSize = true;
            _quickPanelTitleLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
            _quickPanelTitleLabel.ForeColor = Color.White;
            _quickPanelTitleLabel.Location = new Point(18, 36);
            _quickPanelTitleLabel.Text = "ZeroIntrusion";
            ConfigureQuickPanelDragHandle(_quickPanelTitleLabel);

            _quickPanelProfileLabel.AutoEllipsis = true;
            _quickPanelProfileLabel.AutoSize = false;
            _quickPanelProfileLabel.Font = new Font("Segoe UI", 8F);
            _quickPanelProfileLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelProfileLabel.Location = new Point(18, 58);
            _quickPanelProfileLabel.Size = new Size(140, 18);
            _quickPanelProfileLabel.Text = "Profile: Not registered";

            _quickPanelStatusLabel.AutoSize = false;
            _quickPanelStatusLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelStatusLabel.Location = new Point(18, 78);
            _quickPanelStatusLabel.Size = new Size(92, 24);
            _quickPanelStatusLabel.TextAlign = ContentAlignment.MiddleCenter;

            _quickPanelSummaryLabel.AutoSize = false;
            _quickPanelSummaryLabel.Font = new Font("Segoe UI", 8.5F);
            _quickPanelSummaryLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelSummaryLabel.Location = new Point(18, 112);
            _quickPanelSummaryLabel.Size = new Size(140, 56);

            _quickPanelServerStatusPanel.FlowDirection = FlowDirection.TopDown;
            _quickPanelServerStatusPanel.WrapContents = false;
            _quickPanelServerStatusPanel.AutoSize = true;
            _quickPanelServerStatusPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _quickPanelServerStatusPanel.BackColor = Color.Transparent;
            _quickPanelServerStatusPanel.Location = new Point(18, 78);
            _quickPanelServerStatusPanel.Size = new Size(140, 88);
            _quickPanelServerStatusPanel.MinimumSize = new Size(140, 0);
            _quickPanelServerStatusPanel.MaximumSize = new Size(140, 1000);
            _quickPanelServerStatusPanel.Margin = new Padding(0);

            _quickPanelStatusLabel.Visible = false;
            _quickPanelSummaryLabel.Visible = false;

            _quickPanelMenuSectionLabel.AutoSize = true;
            _quickPanelMenuSectionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelMenuSectionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelMenuSectionLabel.Location = new Point(18, 204);
            _quickPanelMenuSectionLabel.Text = "Views";

            _quickPanelNavigationPanel.FlowDirection = FlowDirection.TopDown;
            _quickPanelNavigationPanel.WrapContents = false;
            _quickPanelNavigationPanel.AutoSize = true;
            _quickPanelNavigationPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _quickPanelNavigationPanel.BackColor = Color.Transparent;
            _quickPanelNavigationPanel.Location = new Point(18, 228);
            _quickPanelNavigationPanel.Size = new Size(140, 172);
            _quickPanelNavigationPanel.MinimumSize = new Size(140, 0);
            _quickPanelNavigationPanel.MaximumSize = new Size(140, 1000);
            _quickPanelNavigationPanel.Margin = new Padding(0);

            ConfigureQuickPanelNavigationButton(_quickPanelLoginNavButton, "Login/Logout");
            ConfigureQuickPanelNavigationButton(_quickPanelCredentialsNavButton, "Credentials");
            ConfigureQuickPanelNavigationButton(_quickPanelSettingsNavButton, "Settings");
            ConfigureQuickPanelNavigationButton(_quickPanelFaqNavButton, "FAQ");
            _quickPanelSettingsNavButton.Click += settingsNavButton_Click;

            _quickPanelNavigationPanel.Controls.Add(_quickPanelLoginNavButton);
            _quickPanelNavigationPanel.Controls.Add(_quickPanelCredentialsNavButton);
            _quickPanelNavigationPanel.Controls.Add(_quickPanelSettingsNavButton);
            _quickPanelNavigationPanel.Controls.Add(_quickPanelFaqNavButton);

            _trayMenuController = new TrayMenuController(UpdateQuickPanelNavigationButtonState, HandleTrayMenuSectionChanged);
            _trayMenuController.Register(TrayMenuSection.Login, _quickPanelLoginNavButton);
            _trayMenuController.Register(TrayMenuSection.Credentials, _quickPanelCredentialsNavButton);
            _trayMenuController.Register(TrayMenuSection.Faq, _quickPanelFaqNavButton);

            _quickPanelActionSectionLabel.AutoSize = true;
            _quickPanelActionSectionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelActionSectionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelActionSectionLabel.Location = new Point(18, 492);
            _quickPanelActionSectionLabel.Text = "Actions";

            _quickPanelActionPanel.FlowDirection = FlowDirection.TopDown;
            _quickPanelActionPanel.WrapContents = false;
            _quickPanelActionPanel.AutoSize = true;
            _quickPanelActionPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _quickPanelActionPanel.Location = new Point(18, 516);
            _quickPanelActionPanel.Size = new Size(140, 132);
            _quickPanelActionPanel.MinimumSize = new Size(140, 0);
            _quickPanelActionPanel.MaximumSize = new Size(140, 1000);
            _quickPanelActionPanel.BackColor = Color.Transparent;
            _quickPanelActionPanel.Margin = new Padding(0);

            ConfigureQuickPanelButton(_quickPanelPrimaryActionButton, Color.FromArgb(20, 109, 214), Color.White);
            _quickPanelPrimaryActionButton.Click += quickPanelPrimaryActionButton_Click;

            ConfigureQuickPanelButton(_quickPanelRefreshButton, Color.White, Color.FromArgb(24, 34, 46));
            _quickPanelRefreshButton.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 222);
            _quickPanelRefreshButton.Text = "Refresh";
            _quickPanelRefreshButton.Click += quickPanelRefreshButton_Click;

            ConfigureQuickPanelButton(_quickPanelHideButton, Color.White, Color.FromArgb(24, 34, 46));
            _quickPanelHideButton.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 222);
            ConfigureQuickPanelHeaderIconButton(_quickPanelHideButton, new Point(571, 16), "Minimize", DrawQuickPanelMinimizeIcon);
            _quickPanelHideButton.Click += quickPanelHideButton_Click;

            ConfigureQuickPanelButton(_quickPanelExitButton, Color.FromArgb(45, 51, 60), Color.White);
            ConfigureQuickPanelHeaderIconButton(_quickPanelExitButton, new Point(605, 16), "Exit", DrawQuickPanelExitIcon);
            _quickPanelExitButton.Click += exitToolStripMenuItem_Click;

            _quickPanelActionPanel.Controls.Add(_quickPanelPrimaryActionButton);
            _quickPanelActionPanel.Controls.Add(_quickPanelRefreshButton);
            _quickPanelDetailPanel.Controls.Add(_quickPanelHideButton);
            _quickPanelDetailPanel.Controls.Add(_quickPanelExitButton);

            _quickPanelDetailEyebrowLabel.AutoSize = true;
            _quickPanelDetailEyebrowLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelDetailEyebrowLabel.ForeColor = Color.FromArgb(120, 205, 0);
            _quickPanelDetailEyebrowLabel.Location = new Point(34, 22);
            _quickPanelDetailEyebrowLabel.Text = "SYSTEM STATUS";
            ConfigureQuickPanelDragHandle(_quickPanelDetailEyebrowLabel);

            _quickPanelDetailTitleLabel.AutoSize = true;
            _quickPanelDetailTitleLabel.Font = _quickPanelDetailDefaultTitleFont;
            _quickPanelDetailTitleLabel.ForeColor = Color.White;
            _quickPanelDetailTitleLabel.Location = new Point(32, 52);
            _quickPanelDetailTitleLabel.Text = "Tray overview";
            ConfigureQuickPanelDragHandle(_quickPanelDetailTitleLabel);

            _quickPanelDetailDescriptionLabel.AutoSize = false;
            _quickPanelDetailDescriptionLabel.Font = new Font("Segoe UI", 10F);
            _quickPanelDetailDescriptionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelDetailDescriptionLabel.Location = new Point(36, 98);
            _quickPanelDetailDescriptionLabel.Size = new Size(574, 44);

            _quickPanelSelectionCard.BackColor = Color.FromArgb(41, 43, 71);
            _quickPanelSelectionCard.BorderStyle = BorderStyle.None;
            _quickPanelSelectionCard.Location = new Point(32, 152);
            _quickPanelSelectionCard.Size = new Size(578, 126);

            _quickPanelUserLabel.AutoSize = true;
            _quickPanelUserLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelUserLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelUserLabel.Location = new Point(16, 14);
            _quickPanelUserLabel.Text = "Active profile";

            ConfigureTrayComboBox(_quickPanelUserComboBox, 10F, Color.FromArgb(31, 33, 57));
            _quickPanelUserComboBox.Location = new Point(16, 36);
            _quickPanelUserComboBox.Size = new Size(544, 25);
            _quickPanelUserComboBox.SelectedIndexChanged += quickPanelUserComboBox_SelectedIndexChanged;

            _quickPanelNamespaceLabel.AutoSize = true;
            _quickPanelNamespaceLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelNamespaceLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelNamespaceLabel.Location = new Point(16, 72);
            _quickPanelNamespaceLabel.Text = "Active server";

            ConfigureTrayComboBox(_quickPanelNamespaceComboBox, 10F, Color.FromArgb(31, 33, 57));
            _quickPanelNamespaceComboBox.Location = new Point(16, 92);
            _quickPanelNamespaceComboBox.Size = new Size(544, 25);
            _quickPanelNamespaceComboBox.SelectedIndexChanged += quickPanelNamespaceComboBox_SelectedIndexChanged;

            _quickPanelSettingsUserActionLabel.AutoSize = true;
            _quickPanelSettingsUserActionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelSettingsUserActionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelSettingsUserActionLabel.Location = new Point(16, 14);
            _quickPanelSettingsUserActionLabel.Text = "Account action";
            _quickPanelSettingsUserActionLabel.Visible = false;

            ConfigureTrayComboBox(_quickPanelSettingsUserActionComboBox, 10F, Color.FromArgb(31, 33, 57));
            _quickPanelSettingsUserActionComboBox.Location = new Point(16, 36);
            _quickPanelSettingsUserActionComboBox.Size = new Size(544, 25);
            _quickPanelSettingsUserActionComboBox.Visible = false;
            _quickPanelSettingsUserActionComboBox.SelectedIndexChanged += quickPanelSettingsUserActionComboBox_SelectedIndexChanged;

            _quickPanelSettingsServerActionLabel.AutoSize = true;
            _quickPanelSettingsServerActionLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelSettingsServerActionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelSettingsServerActionLabel.Location = new Point(16, 14);
            _quickPanelSettingsServerActionLabel.Text = "Server action";
            _quickPanelSettingsServerActionLabel.Visible = false;

            ConfigureTrayComboBox(_quickPanelSettingsServerActionComboBox, 10F, Color.FromArgb(31, 33, 57));
            _quickPanelSettingsServerActionComboBox.Location = new Point(16, 36);
            _quickPanelSettingsServerActionComboBox.Size = new Size(544, 25);
            _quickPanelSettingsServerActionComboBox.Visible = false;
            _quickPanelSettingsServerActionComboBox.SelectedIndexChanged += quickPanelSettingsServerActionComboBox_SelectedIndexChanged;

            _quickPanelCredentialGroupSettingsButton.Size = new Size(130, 24);
            _quickPanelCredentialGroupSettingsButton.Location = new Point(130, 10);
            _quickPanelCredentialGroupSettingsButton.FlatStyle = FlatStyle.Flat;
            _quickPanelCredentialGroupSettingsButton.FlatAppearance.BorderSize = 0;
            _quickPanelCredentialGroupSettingsButton.BackColor = Color.FromArgb(46, 48, 77);
            _quickPanelCredentialGroupSettingsButton.ForeColor = Color.FromArgb(120, 205, 0);
            _quickPanelCredentialGroupSettingsButton.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            _quickPanelCredentialGroupSettingsButton.Text = "⚙ Manage groups";
            _quickPanelCredentialGroupSettingsButton.UseVisualStyleBackColor = false;
            _quickPanelCredentialGroupSettingsButton.Visible = false;
            _quickPanelCredentialGroupSettingsButton.Click += quickPanelCredentialManageGroupsButton_Click;

            _quickPanelCredentialEntryCreateIconButton.Size = new Size(150, 24);
            _quickPanelCredentialEntryCreateIconButton.Location = new Point(374, 10);
            _quickPanelCredentialEntryCreateIconButton.FlatStyle = FlatStyle.Flat;
            _quickPanelCredentialEntryCreateIconButton.FlatAppearance.BorderSize = 0;
            _quickPanelCredentialEntryCreateIconButton.BackColor = Color.FromArgb(46, 48, 77);
            _quickPanelCredentialEntryCreateIconButton.ForeColor = Color.FromArgb(120, 205, 0);
            _quickPanelCredentialEntryCreateIconButton.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            _quickPanelCredentialEntryCreateIconButton.Text = "+ Create new entry";
            _quickPanelCredentialEntryCreateIconButton.UseVisualStyleBackColor = false;
            _quickPanelCredentialEntryCreateIconButton.Visible = false;
            _quickPanelCredentialEntryCreateIconButton.Click += quickPanelCredentialCreateButton_Click;

            _quickPanelSelectionCard.Controls.Add(_quickPanelUserLabel);
            _quickPanelSelectionCard.Controls.Add(_quickPanelUserComboBox);
            _quickPanelSelectionCard.Controls.Add(_quickPanelNamespaceLabel);
            _quickPanelSelectionCard.Controls.Add(_quickPanelNamespaceComboBox);
            _quickPanelSelectionCard.Controls.Add(_quickPanelSettingsUserActionLabel);
            _quickPanelSelectionCard.Controls.Add(_quickPanelSettingsUserActionComboBox);
            _quickPanelSelectionCard.Controls.Add(_quickPanelSettingsServerActionLabel);
            _quickPanelSelectionCard.Controls.Add(_quickPanelSettingsServerActionComboBox);
            _quickPanelSelectionCard.Controls.Add(_quickPanelCredentialGroupSettingsButton);
            _quickPanelSelectionCard.Controls.Add(_quickPanelCredentialEntryCreateIconButton);
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

            ConfigureServerRegistrationGuidanceBox();

            ConfigureQuickPanelInlineActionButton(_quickPanelRegisterNewServerInlineButton, "+ Add a server", new Point(16, 14), quickPanelRegisterNewServerInlineButton_Click);
            _quickPanelRegisterNewServerInlineButton.Size = new Size(156, 28);
            _quickPanelRegisterNewServerInlineButton.ForeColor = Color.FromArgb(120, 205, 0);

            ConfigureQuickPanelInlineActionButton(_quickPanelServerRegistrationInlineButton, "+ Sync existing server", new Point(180, 14), quickPanelServerRegistrationInlineButton_Click);
            _quickPanelServerRegistrationInlineButton.Size = new Size(170, 28);
            _quickPanelServerRegistrationInlineButton.ForeColor = Color.FromArgb(120, 205, 0);

            _quickPanelServerStatusValueLabel.AutoSize = false;
            _quickPanelServerStatusValueLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelServerStatusValueLabel.Location = new Point(214, 16);
            _quickPanelServerStatusValueLabel.Size = new Size(108, 24);
            _quickPanelServerStatusValueLabel.TextAlign = ContentAlignment.MiddleCenter;
            _quickPanelServerStatusValueLabel.Visible = false;

            ConfigureQuickPanelInlineActionButton(_quickPanelServerLoginInlineButton, "Login", new Point(438, 14), quickPanelServerLoginInlineButton_Click);
            _quickPanelServerLoginInlineButton.Size = new Size(122, 28);
            _quickPanelServerLoginInlineButton.Visible = false;

            ConfigureCredentialWorkspaceLabel(_quickPanelServerRegistrationUrlLabel, "Namespace url", new Point(16, 50));
            ConfigureCredentialWorkspaceTextBox(_quickPanelServerRegistrationUrlTextBox, new Point(16, 70), new Size(544, 25), multiline: false);

            ConfigureCredentialWorkspaceLabel(_quickPanelServerRegistrationNameLabel, "Namespace name", new Point(300, 50));
            ConfigureCredentialWorkspaceTextBox(_quickPanelServerRegistrationNameTextBox, new Point(300, 70), new Size(260, 25), multiline: false);
            _quickPanelServerRegistrationNameLabel.Visible = false;
            _quickPanelServerRegistrationNameTextBox.Visible = false;

            ConfigureQuickPanelInlineActionButton(_quickPanelServerRegistrationSubmitButton, "Generate QR", new Point(16, 108), quickPanelServerRegistrationSubmitButton_Click);

            ConfigureQuickPanelInlineActionButton(_quickPanelServerRegistrationBackButton, "Back", new Point(16, 12), quickPanelServerRegistrationBackButton_Click);
            _quickPanelServerRegistrationBackButton.Size = new Size(64, 28);
            _quickPanelServerRegistrationBackButton.Visible = false;

            _quickPanelServerRegistrationStatusLabel.AutoSize = false;
            _quickPanelServerRegistrationStatusLabel.Font = new Font("Segoe UI", 8.75F);
            _quickPanelServerRegistrationStatusLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelServerRegistrationStatusLabel.Location = new Point(176, 112);
            _quickPanelServerRegistrationStatusLabel.Size = new Size(384, 32);

            _quickPanelServerRegistrationQrPictureBox.BackColor = Color.White;
            _quickPanelServerRegistrationQrPictureBox.Location = new Point(16, 148);
            _quickPanelServerRegistrationQrPictureBox.Size = new Size(220, 220);
            _quickPanelServerRegistrationQrPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            _quickPanelServerRegistrationQrPictureBox.Visible = false;

            _quickPanelInsightCard.Controls.Add(_quickPanelInsightTitleLabel);
            _quickPanelInsightCard.Controls.Add(_quickPanelInsightBodyLabel);
            _quickPanelInsightCard.Controls.Add(_quickPanelServerRegistrationGuidancePanel);
            _quickPanelInsightCard.Controls.Add(_quickPanelServerRegistrationUrlLabel);
            _quickPanelInsightCard.Controls.Add(_quickPanelServerRegistrationUrlTextBox);
            _quickPanelInsightCard.Controls.Add(_quickPanelServerRegistrationNameLabel);
            _quickPanelInsightCard.Controls.Add(_quickPanelServerRegistrationNameTextBox);
            _quickPanelInsightCard.Controls.Add(_quickPanelServerRegistrationSubmitButton);
            _quickPanelInsightCard.Controls.Add(_quickPanelServerRegistrationBackButton);
            _quickPanelInsightCard.Controls.Add(_quickPanelServerRegistrationStatusLabel);
            _quickPanelInsightCard.Controls.Add(_quickPanelServerRegistrationQrPictureBox);

            InitializeCredentialWorkspacePanel();

            _quickPanelMenuPanel.Controls.Add(_quickPanelEyebrowLabel);
            _quickPanelMenuPanel.Controls.Add(_quickPanelTitleLabel);
            _quickPanelMenuPanel.Controls.Add(_quickPanelProfileLabel);
            _quickPanelMenuPanel.Controls.Add(_quickPanelStatusLabel);
            _quickPanelMenuPanel.Controls.Add(_quickPanelSummaryLabel);
            _quickPanelMenuPanel.Controls.Add(_quickPanelServerStatusPanel);
            _quickPanelMenuPanel.Controls.Add(_quickPanelMenuSectionLabel);
            _quickPanelMenuPanel.Controls.Add(_quickPanelNavigationPanel);
            _quickPanelMenuPanel.Controls.Add(_quickPanelActionSectionLabel);
            _quickPanelMenuPanel.Controls.Add(_quickPanelActionPanel);

            _quickPanelDetailPanel.Controls.Add(_quickPanelDetailEyebrowLabel);
            _quickPanelDetailPanel.Controls.Add(_quickPanelDetailTitleLabel);
            _quickPanelDetailPanel.Controls.Add(_quickPanelDetailDescriptionLabel);
            _quickPanelDetailPanel.Controls.Add(_quickPanelSelectionCard);
            _quickPanelDetailPanel.Controls.Add(_quickPanelMetricsCard);
            _quickPanelDetailPanel.Controls.Add(_quickPanelInsightCard);
            _quickPanelDetailPanel.Controls.Add(_quickPanelSettingsOverviewPanel);
            _quickPanelDetailPanel.Controls.Add(_quickPanelCredentialWorkspacePanel);
            ConfigureQuickPanelDragHandle(_quickPanelDetailPanel);
            InitializeInlineUserRegistrationPanel();
            _quickPanelDetailPanel.Controls.Add(_quickPanelUserRegistrationPanel);
            InitializeInlineUserDeletionPanel();
            _quickPanelDetailPanel.Controls.Add(_quickPanelUserDeletionPanel);

            _quickPanelAccountsPanel.FlowDirection = FlowDirection.TopDown;
            _quickPanelAccountsPanel.WrapContents = false;
            _quickPanelAccountsPanel.AutoScroll = true;
            _quickPanelAccountsPanel.Location = new Point(32, 152);
            _quickPanelAccountsPanel.Size = new Size(578, 390);
            _quickPanelAccountsPanel.Visible = false;
            _quickPanelDetailPanel.Controls.Add(_quickPanelAccountsPanel);

            ConfigureAccountsGuidanceBox();
            _quickPanelDetailPanel.Controls.Add(_quickPanelAccountsGuidancePanel);

            _quickPanelDevicesPanel.FlowDirection = FlowDirection.TopDown;
            _quickPanelDevicesPanel.WrapContents = false;
            _quickPanelDevicesPanel.AutoScroll = true;
            _quickPanelDevicesPanel.Location = new Point(32, 152);
            _quickPanelDevicesPanel.Size = new Size(578, 390);
            _quickPanelDevicesPanel.Visible = false;
            _quickPanelDetailPanel.Controls.Add(_quickPanelDevicesPanel);

            ConfigureQuickPanelDevicesSourceFilter();
            _quickPanelDetailPanel.Controls.Add(_quickPanelDevicesSourceLabel);
            _quickPanelDetailPanel.Controls.Add(_quickPanelDevicesSourceComboBox);

            ConfigureDevicesGuidanceBox();
            _quickPanelDetailPanel.Controls.Add(_quickPanelDevicesGuidancePanel);

            InitializeFaqPanel();
            _quickPanelDetailPanel.Controls.Add(_quickPanelFaqPanel);

            _quickPanelContainer.Controls.Add(_quickPanelMenuPanel);
            _quickPanelContainer.Controls.Add(_quickPanelDividerPanel);
            _quickPanelContainer.Controls.Add(_quickPanelDetailPanel);
            _quickPanelResizeGrip.BackColor = Color.FromArgb(59, 61, 88);
            _quickPanelResizeGrip.Cursor = Cursors.SizeNS;
            _quickPanelResizeGrip.Location = new Point(_quickPanelContainer.Width - 16, _quickPanelContainer.Height - 16);
            _quickPanelResizeGrip.Size = new Size(16, 16);
            _quickPanelResizeGrip.Paint += DrawQuickPanelResizeGripIcon;
            _quickPanelResizeGrip.MouseDown += quickPanelResizeGrip_MouseDown;
            _quickPanelResizeGrip.MouseMove += quickPanelResizeGrip_MouseMove;
            _quickPanelResizeGrip.MouseUp += quickPanelResizeGrip_MouseUp;
            _quickPanelContainer.Controls.Add(_quickPanelResizeGrip);
            _quickPanelResizeGrip.BringToFront();

            ResumeLayout(performLayout: false);
        }

        private void InitializeSettingsOverviewPanel()
        {
            _quickPanelSettingsOverviewPanel.BackColor = Color.Transparent;
            _quickPanelSettingsOverviewPanel.Location = new Point(32, 152);
            _quickPanelSettingsOverviewPanel.Size = new Size(578, 390);
            _quickPanelSettingsOverviewPanel.Visible = false;

            _quickPanelSettingsNavigationPanel.AutoSize = true;
            _quickPanelSettingsNavigationPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _quickPanelSettingsNavigationPanel.BackColor = Color.Transparent;
            _quickPanelSettingsNavigationPanel.FlowDirection = FlowDirection.LeftToRight;
            _quickPanelSettingsNavigationPanel.Location = Point.Empty;
            _quickPanelSettingsNavigationPanel.Margin = Padding.Empty;
            _quickPanelSettingsNavigationPanel.Padding = Padding.Empty;
            _quickPanelSettingsNavigationPanel.WrapContents = false;

            ConfigureSettingsOverviewLink(_quickPanelSettingsOverviewLink, "Overview", TrayMenuSection.Settings);
            ConfigureSettingsOverviewLink(_quickPanelSettingsProfileLink, "Profile", TrayMenuSection.SettingsUsers);
            ConfigureSettingsOverviewLink(_quickPanelSettingsServersLink, "Servers", TrayMenuSection.SettingsServers);
            ConfigureSettingsOverviewLink(_quickPanelSettingsDevicesLink, "Devices", TrayMenuSection.Devices);

            _quickPanelSettingsNavigationPanel.Controls.Add(_quickPanelSettingsOverviewLink);
            _quickPanelSettingsNavigationPanel.Controls.Add(_quickPanelSettingsProfileLink);
            _quickPanelSettingsNavigationPanel.Controls.Add(_quickPanelSettingsServersLink);
            _quickPanelSettingsNavigationPanel.Controls.Add(_quickPanelSettingsDevicesLink);

            _quickPanelSettingsDescriptionBox.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelSettingsDescriptionBox.BorderStyle = BorderStyle.None;
            _quickPanelSettingsDescriptionBox.Cursor = Cursors.Default;
            _quickPanelSettingsDescriptionBox.Font = new Font("Segoe UI", 9.5F);
            _quickPanelSettingsDescriptionBox.ForeColor = Color.FromArgb(220, 222, 241);
            _quickPanelSettingsDescriptionBox.Location = new Point(0, 38);
            _quickPanelSettingsDescriptionBox.ReadOnly = true;
            _quickPanelSettingsDescriptionBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            _quickPanelSettingsDescriptionBox.Size = new Size(578, 337);
            _quickPanelSettingsDescriptionBox.TabStop = false;

            _quickPanelSettingsOverviewPanel.Controls.Add(_quickPanelSettingsNavigationPanel);
            _quickPanelSettingsOverviewPanel.Controls.Add(_quickPanelSettingsDescriptionBox);
        }

        private void ConfigureSettingsOverviewLink(Label label, string text, TrayMenuSection section)
        {
            label.AutoSize = true;
            label.Cursor = Cursors.Hand;
            label.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            label.Margin = new Padding(0, 0, 20, 0);
            label.Text = text;
            label.Click += async (_, _) => await NavigateFromSettingsOverviewAsync(section);
        }

        private void ConfigureQuickPanelNavigationButton(Button button, string text)
        {
            button.Size = new Size(140, 34);
            button.Margin = new Padding(0, 0, 0, 8);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.BorderColor = Color.FromArgb(46, 48, 77);
            button.BackColor = Color.FromArgb(46, 48, 77);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(12, 0, 12, 0);
            button.Text = text;
            button.UseVisualStyleBackColor = false;
        }

        private void ConfigureQuickPanelHeaderIconButton(Button button, Point location, string toolTipText, PaintEventHandler paintHandler)
        {
            button.AccessibleName = toolTipText;
            button.Location = location;
            button.Size = new Size(28, 28);
            button.Margin = Padding.Empty;
            button.Padding = Padding.Empty;
            button.Text = string.Empty;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.BackColor = Color.Transparent;
            button.ForeColor = Color.White;
            button.FlatAppearance.MouseOverBackColor = Color.Transparent;
            button.FlatAppearance.MouseDownBackColor = Color.Transparent;
            button.Paint += paintHandler;
            button.MouseEnter += (_, _) =>
            {
                button.ForeColor = Color.FromArgb(120, 205, 0);
            };
            button.MouseLeave += (_, _) =>
            {
                button.ForeColor = Color.White;
            };
            _quickPanelToolTip.SetToolTip(button, toolTipText);
        }

        private static void DrawQuickPanelMinimizeIcon(object? sender, PaintEventArgs eventArgs)
        {
            if (sender is not Button button)
            {
                return;
            }

            using var pen = new Pen(button.ForeColor, 2F);
            int centerY = button.ClientRectangle.Height / 2 + 4;
            eventArgs.Graphics.DrawLine(pen, 8, centerY, button.ClientRectangle.Width - 8, centerY);
        }

        private static void DrawQuickPanelExitIcon(object? sender, PaintEventArgs eventArgs)
        {
            if (sender is not Button button)
            {
                return;
            }

            using var pen = new Pen(button.ForeColor, 2F);
            Rectangle bounds = button.ClientRectangle;
            eventArgs.Graphics.DrawLine(pen, 9, 9, bounds.Width - 9, bounds.Height - 9);
            eventArgs.Graphics.DrawLine(pen, bounds.Width - 9, 9, 9, bounds.Height - 9);
        }

        private static void DrawQuickPanelResizeGripIcon(object? sender, PaintEventArgs eventArgs)
        {
            if (sender is not Control grip)
            {
                return;
            }

            using var pen = new Pen(Color.FromArgb(171, 174, 211), 1.5F);
            int right = grip.ClientRectangle.Width - 3;
            int bottom = grip.ClientRectangle.Height - 3;

            eventArgs.Graphics.DrawLine(pen, 5, bottom, right, 5);
            eventArgs.Graphics.DrawLine(pen, 8, bottom, right, 8);
            eventArgs.Graphics.DrawLine(pen, 11, bottom, right, 11);
        }

        private void settingsNavButton_Click(object? sender, EventArgs e)
        {
            _settingsAccessMessage = string.Empty;
            _trayMenuController?.SetActiveSection(TrayMenuSection.Settings, triggerStateChanged: true);
        }

        private async Task NavigateFromSettingsOverviewAsync(TrayMenuSection section)
        {
            if (RequiresSettingsAuthentication(section) && !_authenticationService.IsAuthorized)
            {
                ShowSettingsAccessMessage("Log in to access Servers, Devices, and Account settings.");
                return;
            }

            if (section != TrayMenuSection.Devices)
            {
                _settingsAccessMessage = string.Empty;
                _trayMenuController?.SetActiveSection(section, triggerStateChanged: true);
                return;
            }

            _settingsAccessMessage = string.Empty;
            _trayMenuController?.SetActiveSection(TrayMenuSection.Devices, triggerStateChanged: true);
            await LoadAccountDevicesAsync();
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

        private static void ConfigureQuickPanelButton(Button button, Color backColor, Color foreColor)
        {
            button.Size = new Size(140, 30);
            button.Margin = new Padding(0, 0, 0, 8);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.BorderColor = Color.FromArgb(46, 48, 77);
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(12, 0, 12, 0);
            button.UseVisualStyleBackColor = false;
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

        private void InitializeCredentialWorkspacePanel()
        {
            _quickPanelCredentialWorkspacePanel.Location = new Point(32, 226);
            _quickPanelCredentialWorkspacePanel.Size = new Size(578, 414);
            _quickPanelCredentialWorkspacePanel.BackColor = Color.FromArgb(41, 43, 71);
            _quickPanelCredentialWorkspacePanel.AutoScroll = false;
            _quickPanelCredentialWorkspacePanel.Visible = false;

            ConfigureCredentialWorkspaceButton(_quickPanelCredentialBackButton, "Back", new Point(16, 12), quickPanelCredentialBackButton_Click);
            _quickPanelCredentialBackButton.Visible = false;

            _quickPanelCredentialEntryEditorPanel.Location = new Point(16, 12);
            _quickPanelCredentialEntryEditorPanel.Size = new Size(547, 414);
            _quickPanelCredentialEntryEditorPanel.BackColor = Color.Transparent;

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialEditorModeLabel, "Read / Edit / Delete mode", new Point(0, 0));
            _quickPanelCredentialEditorModeLabel.ForeColor = Color.FromArgb(120, 205, 0);
            _quickPanelCredentialEditorModeLabel.Visible = false;

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialLabelEditorLabel, "Label", new Point(0, 20));
            ConfigureCredentialWorkspaceTextBox(_quickPanelCredentialLabelTextBox, new Point(0, 40), new Size(257, 25), multiline: false);
            _quickPanelCredentialLabelTextBox.TextChanged += credentialEditorInput_TextChanged;

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialAssignedGroupLabel, "Assigned group", new Point(272, 20));
            ConfigureCredentialWorkspaceComboBox(_quickPanelCredentialAssignedGroupComboBox, new Point(272, 40), new Size(275, 25));
            _quickPanelCredentialAssignedGroupComboBox.SelectedIndexChanged += credentialEditorInput_Changed;

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialDomainLabel, "Domain", new Point(0, 78));
            ConfigureCredentialWorkspaceTextBox(_quickPanelCredentialDomainTextBox, new Point(0, 98), new Size(166, 25), multiline: false);
            _quickPanelCredentialDomainTextBox.TextChanged += credentialEditorInput_TextChanged;

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialUsernameLabel, "Username", new Point(178, 78));
            ConfigureCredentialWorkspaceTextBox(_quickPanelCredentialUsernameTextBox, new Point(178, 98), new Size(166, 25), multiline: false);
            _quickPanelCredentialUsernameTextBox.TextChanged += credentialEditorInput_TextChanged;
            ConfigureCredentialWorkspaceIconButton(_quickPanelCredentialUsernameCopyButton, "⧉", new Point(250, 75), quickPanelCredentialUsernameCopyButton_Click);

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialPasswordLabel, "Password", new Point(356, 78));
            ConfigureCredentialWorkspaceTextBox(_quickPanelCredentialPasswordTextBox, new Point(356, 98), new Size(191, 25), multiline: false);
            _quickPanelCredentialPasswordTextBox.UseSystemPasswordChar = true;
            _quickPanelCredentialPasswordTextBox.TextChanged += credentialEditorInput_TextChanged;
            ConfigureCredentialWorkspaceIconButton(_quickPanelCredentialPasswordGenerateButton, "↻", new Point(425, 75), quickPanelCredentialPasswordGenerateButton_Click);
            ConfigureCredentialWorkspaceIconButton(_quickPanelCredentialPasswordRevealButton, "👁", new Point(501, 75), quickPanelCredentialPasswordRevealButton_Click);
            ConfigureCredentialWorkspaceIconButton(_quickPanelCredentialPasswordCopyButton, "⧉", new Point(525, 75), quickPanelCredentialPasswordCopyButton_Click);

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialDescriptionLabel, "Description", new Point(0, 140));
            ConfigureCredentialWorkspaceTextBox(_quickPanelCredentialDescriptionTextBox, new Point(0, 160), new Size(547, 135), multiline: true);
            _quickPanelCredentialDescriptionTextBox.TextChanged += credentialEditorInput_TextChanged;
            ConfigureCredentialWorkspaceIconButton(_quickPanelCredentialDescriptionCopyButton, "⧉", new Point(88, 137), quickPanelCredentialDescriptionCopyButton_Click);

            ConfigureCredentialWorkspaceButton(_quickPanelCredentialDeleteButton, "Delete entry", new Point(0, 311), quickPanelCredentialDeleteButton_Click);
            _quickPanelCredentialDeleteButton.BackColor = Color.FromArgb(214, 88, 88);
            _quickPanelCredentialDeleteButton.ForeColor = Color.White;

            ConfigureCredentialWorkspaceButton(_quickPanelCredentialSaveButton, "Save changes", new Point(112, 311), quickPanelCredentialSaveButton_Click);
            _quickPanelCredentialSaveButton.BackColor = Color.FromArgb(120, 205, 0);
            _quickPanelCredentialSaveButton.ForeColor = Color.Black;
            _quickPanelCredentialSaveButton.FlatAppearance.BorderSize = 1;
            _quickPanelCredentialSaveButton.FlatAppearance.BorderColor = Color.FromArgb(120, 205, 0);

            _quickPanelCredentialStatusLabel.AutoSize = false;
            _quickPanelCredentialStatusLabel.Font = new Font("Segoe UI", 8.75F);
            _quickPanelCredentialStatusLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelCredentialStatusLabel.Location = new Point(0, 351);
            _quickPanelCredentialStatusLabel.Size = new Size(547, 32);

            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialEditorModeLabel);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialLabelEditorLabel);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialLabelTextBox);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialAssignedGroupLabel);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialAssignedGroupComboBox);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialDomainLabel);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialDomainTextBox);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialUsernameLabel);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialUsernameTextBox);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialUsernameCopyButton);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialPasswordLabel);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialPasswordTextBox);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialPasswordGenerateButton);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialPasswordRevealButton);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialPasswordCopyButton);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialDescriptionLabel);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialDescriptionTextBox);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialDescriptionCopyButton);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialDeleteButton);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialSaveButton);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialStatusLabel);

            _quickPanelCredentialGroupEditorPanel.Location = new Point(16, 12);
            _quickPanelCredentialGroupEditorPanel.Size = new Size(530, 280);
            _quickPanelCredentialGroupEditorPanel.BackColor = Color.Transparent;
            _quickPanelCredentialGroupEditorPanel.Visible = false;

            ConfigureCredentialWorkspaceLabel(_quickPanelManagedGroupLabel, "Groups", new Point(0, 0));
            ConfigureCredentialWorkspaceComboBox(_quickPanelManagedGroupComboBox, new Point(0, 20), new Size(522, 25));
            _quickPanelManagedGroupComboBox.SelectedIndexChanged += quickPanelManagedGroupComboBox_SelectedIndexChanged;

            ConfigureCredentialWorkspaceLabel(_quickPanelManagedGroupNameLabel, "Group name", new Point(0, 58));
            ConfigureCredentialWorkspaceTextBox(_quickPanelManagedGroupNameTextBox, new Point(0, 78), new Size(522, 25), multiline: false);
            _quickPanelManagedGroupNameTextBox.TextChanged += groupEditorInput_TextChanged;

            ConfigureCredentialWorkspaceLabel(_quickPanelNewGroupNameLabel, "New group name", new Point(0, 58));
            ConfigureCredentialWorkspaceTextBox(_quickPanelNewGroupNameTextBox, new Point(0, 78), new Size(522, 25), multiline: false);
            _quickPanelNewGroupNameTextBox.TextChanged += groupEditorInput_TextChanged;

            ConfigureCredentialWorkspaceButton(_quickPanelCredentialGroupDeleteButton, "Delete selected group", new Point(0, 122), quickPanelCredentialGroupDeleteButton_Click);
            _quickPanelCredentialGroupDeleteButton.Size = new Size(150, 28);
            _quickPanelCredentialGroupDeleteButton.BackColor = Color.FromArgb(214, 88, 88);
            _quickPanelCredentialGroupDeleteButton.ForeColor = Color.White;
            ConfigureCredentialWorkspaceButton(_quickPanelCredentialGroupSaveButton, "Save group", new Point(158, 122), quickPanelCredentialGroupSaveButton_Click);
            ConfigureCredentialWorkspaceButton(_quickPanelCredentialGroupCreateButton, "Create group", new Point(270, 122), quickPanelCredentialGroupCreateButton_Click);

            _quickPanelGroupStatusLabel.AutoSize = false;
            _quickPanelGroupStatusLabel.Font = new Font("Segoe UI", 8.75F);
            _quickPanelGroupStatusLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelGroupStatusLabel.Location = new Point(0, 162);
            _quickPanelGroupStatusLabel.Size = new Size(522, 40);

            _quickPanelGroupDeletionInfoPanel.BackColor = Color.FromArgb(46, 48, 77);
            _quickPanelGroupDeletionInfoPanel.BorderStyle = BorderStyle.FixedSingle;
            _quickPanelGroupDeletionInfoPanel.Location = new Point(0, 212);
            _quickPanelGroupDeletionInfoPanel.Size = new Size(522, 54);

            _quickPanelGroupDeletionInfoLabel.AutoSize = false;
            _quickPanelGroupDeletionInfoLabel.Font = new Font("Segoe UI", 8.5F);
            _quickPanelGroupDeletionInfoLabel.ForeColor = Color.FromArgb(220, 222, 241);
            _quickPanelGroupDeletionInfoLabel.Location = new Point(10, 7);
            _quickPanelGroupDeletionInfoLabel.Size = new Size(500, 38);
            _quickPanelGroupDeletionInfoLabel.Text = "Deleting a group only removes the selected group. Credentials assigned to it move to the Ungrouped category.";
            _quickPanelGroupDeletionInfoPanel.Controls.Add(_quickPanelGroupDeletionInfoLabel);

            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelManagedGroupLabel);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelManagedGroupComboBox);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelManagedGroupNameLabel);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelManagedGroupNameTextBox);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelNewGroupNameLabel);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelNewGroupNameTextBox);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelCredentialGroupSaveButton);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelCredentialGroupDeleteButton);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelCredentialGroupCreateButton);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelGroupStatusLabel);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelGroupDeletionInfoPanel);

            _quickPanelCredentialWorkspacePanel.Controls.Add(_quickPanelCredentialBackButton);
            _quickPanelCredentialWorkspacePanel.Controls.Add(_quickPanelCredentialEntryEditorPanel);
            _quickPanelCredentialWorkspacePanel.Controls.Add(_quickPanelCredentialGroupEditorPanel);
        }

        private static void ConfigureCredentialWorkspaceLabel(Label label, string text, Point location)
        {
            label.AutoSize = true;
            label.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(171, 174, 211);
            label.Location = location;
            label.Text = text;
        }

        private void InitializeInlineUserRegistrationPanel()
        {
            _quickPanelUserRegistrationPanel.BackColor = Color.FromArgb(41, 43, 71);
            _quickPanelUserRegistrationPanel.Location = new Point(32, 152);
            _quickPanelUserRegistrationPanel.Size = new Size(578, 388);
            _quickPanelUserRegistrationPanel.Visible = false;

            ConfigureCredentialWorkspaceLabel(_quickPanelUserRegistrationUrlLabel, "Base url", new Point(16, 18));
            ConfigureCredentialWorkspaceTextBox(_quickPanelUserRegistrationUrlTextBox, new Point(16, 38), new Size(544, 25), multiline: false);
            ConfigureCredentialWorkspaceLabel(_quickPanelUserRegistrationNameLabel, "Profile name", new Point(16, 82));
            ConfigureCredentialWorkspaceTextBox(_quickPanelUserRegistrationNameTextBox, new Point(16, 102), new Size(544, 25), multiline: false);

            ConfigureCredentialWorkspaceButton(_quickPanelUserRegistrationBackButton, "Back", new Point(16, 148), quickPanelUserRegistrationBackButton_Click);
            _quickPanelUserRegistrationBackButton.Size = new Size(90, 30);
            ConfigureCredentialWorkspaceButton(
                _quickPanelUserRegistrationSubmitButton,
                "Continue",
                new Point(_quickPanelUserRegistrationBackButton.Right + 5, _quickPanelUserRegistrationBackButton.Top),
                quickPanelUserRegistrationSubmitButton_Click);
            _quickPanelUserRegistrationSubmitButton.Size = new Size(120, 30);

            _quickPanelUserRegistrationStatusLabel.AutoSize = false;
            _quickPanelUserRegistrationStatusLabel.Font = new Font("Segoe UI", 8.75F);
            _quickPanelUserRegistrationStatusLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelUserRegistrationStatusLabel.Location = new Point(16, 194);
            _quickPanelUserRegistrationStatusLabel.Size = new Size(316, 32);

            _quickPanelUserRegistrationQrPictureBox.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelUserRegistrationQrPictureBox.BorderStyle = BorderStyle.FixedSingle;
            _quickPanelUserRegistrationQrPictureBox.Location = new Point(348, 148);
            _quickPanelUserRegistrationQrPictureBox.Size = new Size(212, 212);
            _quickPanelUserRegistrationQrPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            _quickPanelUserRegistrationQrPictureBox.Visible = false;

            _quickPanelUserRegistrationPanel.Controls.Add(_quickPanelUserRegistrationUrlLabel);
            _quickPanelUserRegistrationPanel.Controls.Add(_quickPanelUserRegistrationUrlTextBox);
            _quickPanelUserRegistrationPanel.Controls.Add(_quickPanelUserRegistrationNameLabel);
            _quickPanelUserRegistrationPanel.Controls.Add(_quickPanelUserRegistrationNameTextBox);
            _quickPanelUserRegistrationPanel.Controls.Add(_quickPanelUserRegistrationSubmitButton);
            _quickPanelUserRegistrationPanel.Controls.Add(_quickPanelUserRegistrationBackButton);
            _quickPanelUserRegistrationPanel.Controls.Add(_quickPanelUserRegistrationStatusLabel);
            _quickPanelUserRegistrationPanel.Controls.Add(_quickPanelUserRegistrationQrPictureBox);
        }

        private void InitializeInlineUserDeletionPanel()
        {
            _quickPanelUserDeletionPanel.BackColor = Color.FromArgb(41, 43, 71);
            _quickPanelUserDeletionPanel.Location = new Point(32, 152);
            _quickPanelUserDeletionPanel.Size = new Size(578, 220);
            _quickPanelUserDeletionPanel.Visible = false;

            _quickPanelUserDeletionMessageLabel.AutoSize = false;
            _quickPanelUserDeletionMessageLabel.Font = new Font("Segoe UI", 10F);
            _quickPanelUserDeletionMessageLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelUserDeletionMessageLabel.Location = new Point(16, 20);
            _quickPanelUserDeletionMessageLabel.Size = new Size(544, 104);
            _quickPanelUserDeletionMessageLabel.Text = "Deleting your profile removes this machine's local registration from this app. It does not delete any account or credential. The device will be signed out of every known server. You can add this machine to your account again at any time.";

            _quickPanelUserDeletionProfileLabel.AutoSize = false;
            _quickPanelUserDeletionProfileLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelUserDeletionProfileLabel.ForeColor = Color.White;
            _quickPanelUserDeletionProfileLabel.Location = new Point(16, 130);
            _quickPanelUserDeletionProfileLabel.Size = new Size(544, 20);

            ConfigureCredentialWorkspaceButton(_quickPanelUserDeletionCancelButton, "Cancel", new Point(16, 162), quickPanelUserDeletionCancelButton_Click);
            _quickPanelUserDeletionCancelButton.Size = new Size(90, 30);
            ConfigureCredentialWorkspaceButton(_quickPanelUserDeletionConfirmButton, "Delete profile", new Point(112, 162), quickPanelUserDeletionConfirmButton_Click);
            _quickPanelUserDeletionConfirmButton.Size = new Size(120, 30);
            _quickPanelUserDeletionConfirmButton.BackColor = Color.FromArgb(214, 88, 88);
            _quickPanelUserDeletionConfirmButton.ForeColor = Color.White;
            _quickPanelUserDeletionConfirmButton.FlatAppearance.BorderColor = Color.FromArgb(214, 88, 88);

            _quickPanelUserDeletionPanel.Controls.Add(_quickPanelUserDeletionMessageLabel);
            _quickPanelUserDeletionPanel.Controls.Add(_quickPanelUserDeletionProfileLabel);
            _quickPanelUserDeletionPanel.Controls.Add(_quickPanelUserDeletionCancelButton);
            _quickPanelUserDeletionPanel.Controls.Add(_quickPanelUserDeletionConfirmButton);
        }

        private static void ConfigureCredentialWorkspaceTextBox(TextBox textBox, Point location, Size size, bool multiline)
        {
            textBox.Location = location;
            textBox.Size = size;
            textBox.Multiline = multiline;
            textBox.ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None;
            textBox.Font = new Font("Segoe UI", 9.5F);
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.BackColor = Color.FromArgb(41, 43, 71);
            textBox.ForeColor = Color.White;
            ApplyTextBoxHorizontalPadding(textBox, TextBoxHorizontalPadding);
        }

        private static void ApplyTextBoxHorizontalPadding(TextBox textBox, int padding)
        {
            void ApplyMargins()
            {
                int margins = padding | (padding << 16);
                SendMessage(textBox.Handle, EmSetMargins, (IntPtr)(EcLeftMargin | EcRightMargin), (IntPtr)margins);
            }

            if (textBox.IsHandleCreated)
            {
                ApplyMargins();
            }

            textBox.HandleCreated += (_, _) => ApplyMargins();
        }

        private static void ConfigureCredentialWorkspaceComboBox(TrayDropdown comboBox, Point location, Size size)
        {
            ConfigureTrayComboBox(comboBox, 9.5F, Color.FromArgb(41, 43, 71));
            comboBox.Location = location;
            comboBox.Size = size;
        }

        private static void ConfigureTrayComboBox(TrayDropdown comboBox, float fontSize, Color backColor)
        {
            comboBox.Font = new Font("Segoe UI", fontSize);
            comboBox.BackColor = backColor;
            comboBox.ForeColor = Color.White;
            comboBox.DropDownHeight = 220;
        }

        private void ConfigureQuickPanelDevicesSourceFilter()
        {
            _quickPanelDevicesSourceLabel.AutoSize = true;
            _quickPanelDevicesSourceLabel.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            _quickPanelDevicesSourceLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelDevicesSourceLabel.Text = "Server URL";
            _quickPanelDevicesSourceLabel.Visible = false;

            ConfigureTrayComboBox(_quickPanelDevicesSourceComboBox, 9F, Color.FromArgb(41, 43, 71));
            _quickPanelDevicesSourceComboBox.Visible = false;
            _quickPanelDevicesSourceComboBox.SelectedIndexChanged += quickPanelDevicesSourceComboBox_SelectedIndexChanged;
        }

        private void ApplyDevicesSourceFilterLayout()
        {
            const int verticalGap = 15;

            int labelTop = _quickPanelDevicesGuidancePanel.Bottom + verticalGap;
            _quickPanelDevicesSourceLabel.Location = new Point(32, labelTop);
            _quickPanelDevicesSourceComboBox.Location = new Point(108, labelTop - 4);
            _quickPanelDevicesSourceComboBox.Size = new Size(502, 25);

            int listTop = _quickPanelDevicesSourceComboBox.Bottom + verticalGap;
            _quickPanelDevicesPanel.Location = new Point(QuickPanelGuidanceLeft, listTop);
            _quickPanelDevicesPanel.Height = _quickPanelDetailPanel.Height - _quickPanelDevicesPanel.Top - QuickPanelContentBottomMargin;
        }

        private static void ConfigureCredentialWorkspaceButton(Button button, string text, Point location, EventHandler clickHandler)
        {
            button.Size = new Size(104, 28);
            button.Location = location;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(101, 106, 146);
            button.BackColor = Color.FromArgb(67, 72, 106);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            button.Text = text;
            button.UseVisualStyleBackColor = false;
            button.Click += clickHandler;
        }

        private static void ConfigureQuickPanelInlineActionButton(Button button, string text, Point location, EventHandler clickHandler)
        {
            button.Size = new Size(152, 28);
            button.Location = location;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(101, 106, 146);
            button.BackColor = Color.FromArgb(67, 72, 106);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            button.Text = text;
            button.UseVisualStyleBackColor = false;
            button.Click += clickHandler;
        }

        private static void ConfigureCredentialWorkspaceIconButton(Button button, string text, Point location, EventHandler clickHandler)
        {
            button.Size = new Size(22, 25);
            button.Location = location;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(46, 48, 77);
            button.ForeColor = Color.FromArgb(120, 205, 0);
            button.Font = new Font("Segoe UI Symbol", 9F, FontStyle.Bold);
            button.Text = text;
            button.UseVisualStyleBackColor = false;
            button.Click += clickHandler;
        }

        private void ApplyRegistrationLayout()
        {
            CloseQuickPanel();
            _quickPanelContainer.Visible = false;
            SetRegistrationControlsVisible(true);
            ApplyRegistrationTheme();
            ClientSize = new Size(800, 450);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = !_registrationHostedInTray;
            Text = "ZeroIntrusion Registration";
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

            pictureBoxQr.BackColor = Color.FromArgb(41, 43, 71);
            pictureBoxQr.BorderStyle = BorderStyle.FixedSingle;
        }

        private void ApplyQuickPanelLayout()
        {
            _quickPanelContainer.Visible = true;
            SetRegistrationControlsVisible(false);
            ApplyQuickPanelHeight(_quickPanelHeight);
        }

        private void quickPanelResizeGrip_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !IsQuickPanelOpen())
            {
                return;
            }

            BeginQuickPanelResize(_quickPanelResizeGrip);
        }

        private void quickPanelResizeGrip_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_quickPanelResizing)
            {
                return;
            }

            ResizeQuickPanel();
        }

        private void quickPanelResizeGrip_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                EndQuickPanelResize();
            }
        }

        private void BeginQuickPanelResize(Control captureTarget)
        {
            _quickPanelResizing = true;
            _quickPanelResizeStartHeight = _quickPanelContainer.Height;
            _quickPanelResizeStartScreenY = Control.MousePosition.Y;
            captureTarget.Capture = true;
        }

        private void ResizeQuickPanel()
        {
            ApplyQuickPanelHeight(_quickPanelResizeStartHeight + Control.MousePosition.Y - _quickPanelResizeStartScreenY);
        }

        private void EndQuickPanelResize()
        {
            _quickPanelResizing = false;
            _quickPanelResizeGrip.Capture = false;
            _quickPanelContainer.Capture = false;
        }

        private void ApplyQuickPanelHeight(int requestedHeight)
        {
            Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            int maximumHeight = Math.Max(450, workingArea.Height - 16);
            _quickPanelHeight = Math.Clamp(requestedHeight, 450, maximumHeight);
            _quickPanelContainer.Height = _quickPanelHeight;
            _quickPanelMenuPanel.Height = _quickPanelHeight;
            _quickPanelDividerPanel.Height = _quickPanelHeight;
            _quickPanelDetailPanel.Height = _quickPanelHeight;
            _quickPanelResizeGrip.Location = new Point(
                _quickPanelContainer.Width - _quickPanelResizeGrip.Width,
                _quickPanelHeight - _quickPanelResizeGrip.Height);
            LayoutCredentialEntryEditor();
            LayoutSettingsOverview();
            ApplyGuidanceListLayout(_quickPanelAccountsGuidancePanel, _quickPanelAccountsPanel);
            ApplyGuidanceListLayout(_quickPanelDevicesGuidancePanel, _quickPanelDevicesPanel);
            ApplyDevicesSourceFilterLayout();

            if (_quickPanelHost != null)
            {
                _quickPanelHost.Size = _quickPanelContainer.Size;
            }

            if (IsQuickPanelOpen())
            {
                ConfigureTrayMenuPresentation();
                _lastTrayMenuScreenPoint = ClampQuickPanelScreenPoint(contextMenuStrip1.Location);
                contextMenuStrip1.Location = _lastTrayMenuScreenPoint;
            }
        }

        private void LayoutCredentialEntryEditor()
        {
            const int bottomMargin = 15;
            const int actionToStatusGap = 12;
            const int descriptionToActionGap = 16;

            int workspaceHeight = Math.Max(
                414,
                _quickPanelDetailPanel.Height - _quickPanelCredentialWorkspacePanel.Top - bottomMargin);
            _quickPanelCredentialWorkspacePanel.Height = workspaceHeight;
            _quickPanelCredentialEntryEditorPanel.Height = workspaceHeight - _quickPanelCredentialEntryEditorPanel.Top;

            _quickPanelCredentialStatusLabel.Top = _quickPanelCredentialEntryEditorPanel.Height -
                _quickPanelCredentialStatusLabel.Height - bottomMargin;
            int actionTop = _quickPanelCredentialStatusLabel.Top -
                _quickPanelCredentialSaveButton.Height - actionToStatusGap;
            _quickPanelCredentialSaveButton.Top = actionTop;
            _quickPanelCredentialDeleteButton.Top = actionTop;
            if (_quickPanelCredentialEntryEditorPanel.Visible)
            {
                _quickPanelCredentialBackButton.Location = new Point(
                    _quickPanelCredentialEntryEditorPanel.Left + _quickPanelCredentialSaveButton.Right + 8,
                    _quickPanelCredentialEntryEditorPanel.Top + actionTop);
            }

            _quickPanelCredentialDescriptionTextBox.Height = Math.Max(
                80,
                actionTop - _quickPanelCredentialDescriptionTextBox.Top - descriptionToActionGap);
        }

        private void LayoutSettingsOverview()
        {
            const int bottomMargin = 15;
            if (_trayMenuController?.ActiveSection != TrayMenuSection.Settings)
            {
                _quickPanelSettingsOverviewPanel.Height = _quickPanelSettingsNavigationPanel.Height;
                return;
            }

            int overviewHeight = Math.Max(
                220,
                _quickPanelDetailPanel.Height - _quickPanelSettingsOverviewPanel.Top - bottomMargin);

            _quickPanelSettingsOverviewPanel.Height = overviewHeight;
            _quickPanelSettingsDescriptionBox.Height = overviewHeight - _quickPanelSettingsDescriptionBox.Top;
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
        }

        private void ShowQuickPanel(bool refreshFromBackend = true)
        {
            if (!_isDeviceRegistered)
            {
                ShowBaseUrlPrompt(showAsTrayPrompt: true);
                return;
            }

            _trayMenuController?.SetActiveSection(TrayMenuSection.Login);
            ApplyQuickPanelLayout();
            RefreshQuickPanelSelections();
            UpdateQuickPanelState();

            if (IsQuickPanelOpen())
            {
                if (refreshFromBackend)
                {
                    _ = RefreshQuickPanelFromBackendAsync();
                }

                return;
            }

            ShowExpandedTrayMenu();

            if (refreshFromBackend)
            {
                _ = RefreshQuickPanelFromBackendAsync();
            }
        }

        private void HandleTrayMenuSectionChanged()
        {
            if (_trayMenuController?.ActiveSection != TrayMenuSection.Credentials)
            {
                RefreshQuickPanelSelections();
            }

            UpdateQuickPanelState();

            if (_trayMenuController?.ActiveSection == TrayMenuSection.Credentials && _authenticationService.IsAuthorizedForActiveNamespace)
            {
                _ = EnsureCredentialCollectionLoadedAsync(forceRefresh: true);
                return;
            }

            if (_trayMenuController?.ActiveSection == TrayMenuSection.Devices)
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

            try
            {
                _quickPanelUserComboBox.Items.Clear();
                _quickPanelNamespaceComboBox.Items.Clear();

                if (!_registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) ||
                    registrations == null ||
                    registrations.Count == 0)
                {
                    _quickPanelUserComboBox.Enabled = false;
                    _quickPanelNamespaceComboBox.Enabled = false;
                    return;
                }

                string? activeUserId = AppEnvironment.GetActiveUserId();
                DesktopRegistration activeRegistration = registrations.FirstOrDefault(registration =>
                        string.Equals(registration.UserKey, activeUserId, StringComparison.Ordinal))
                    ?? registrations[0];

                foreach (DesktopRegistration registration in registrations)
                {
                    _quickPanelUserComboBox.Items.Add(new SelectionOption(registration.DisplayUserName, registration.UserKey));
                }

                SelectComboBoxValue(_quickPanelUserComboBox, activeRegistration.UserKey);
                _quickPanelUserComboBox.Enabled = registrations.Count > 1;

                string? activeNamespaceId = AppEnvironment.GetActiveNamespaceId();
                foreach (NamespaceItemDto namespaceItem in activeRegistration.Namespaces)
                {
                    _quickPanelNamespaceComboBox.Items.Add(new SelectionOption(namespaceItem.Namespace, namespaceItem.NamespaceId));
                }

                if (activeRegistration.Namespaces.Count > 0)
                {
                    string selectedNamespaceId = activeRegistration.Namespaces.FirstOrDefault(namespaceItem =>
                            string.Equals(namespaceItem.NamespaceId, activeNamespaceId, StringComparison.Ordinal))?.NamespaceId
                        ?? activeRegistration.Namespaces[0].NamespaceId;
                    SelectComboBoxValue(_quickPanelNamespaceComboBox, selectedNamespaceId);
                }

                _quickPanelNamespaceComboBox.Enabled = activeRegistration.Namespaces.Count > 1;
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
                    comboBox.SetSelectedIndexSilently(index);
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
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
            _quickPanelStatusLabel.Text = state.StatusText;
            _quickPanelStatusLabel.BackColor = state.StatusBackColor;
            _quickPanelStatusLabel.ForeColor = state.StatusForeColor;
            _quickPanelServerStatusValueLabel.Text = state.ActiveNamespaceStatusText;
            _quickPanelServerStatusValueLabel.BackColor = state.ActiveNamespaceStatusBackColor;
            _quickPanelServerStatusValueLabel.ForeColor = state.ActiveNamespaceStatusForeColor;
            _quickPanelSummaryLabel.Text = BuildQuickPanelSummary(state.StatusText, state.ActiveNamespace);
            _quickPanelProfileLabel.Text = $"Profile: {state.Registration?.DisplayUserName ?? "Not registered"}";
            RefreshQuickPanelServerStatusList(state.Registration);

            _quickPanelAccountValueLabel.Text = state.Registration?.DisplayUserName ?? "Not registered";
            _quickPanelDeviceValueLabel.Text = state.Registration?.DeviceId ?? "Waiting for pairing";
            _quickPanelConnectionValueLabel.Text = BuildConnectionSummary();
            _quickPanelLastUpdatedValueLabel.Text = _quickPanelRefreshInProgress
                ? "Refreshing..."
                : _lastTrayStateUpdatedAt.LocalDateTime.ToString("HH:mm:ss");

            ApplyQuickPanelSectionAvailability();
            ApplyQuickPanelViewState(BuildQuickPanelViewState(state.Registration, state.ActiveNamespace, state.StatusText));
            UpdateAuthenticationMenuItems();
            UpdateTrayTooltip(state.Registration, state.ActiveNamespace, state.StatusText);
        }

        private void ApplyQuickPanelSectionAvailability()
        {
            bool hasAuthenticatedUser = _authenticationService.IsAuthorized;
            bool credentialsSectionEnabled = _authenticationService.IsAuthorizedForActiveNamespace;

            _quickPanelCredentialsNavButton.Visible = _isDeviceRegistered;
            _quickPanelSettingsNavButton.Visible = _isDeviceRegistered;

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

            _quickPanelCredentialsNavButton.Enabled = credentialsSectionEnabled;
            _quickPanelCredentialsNavButton.Cursor = credentialsSectionEnabled ? Cursors.Hand : Cursors.Default;

            if (_trayMenuController == null)
            {
                return;
            }

            SetQuickPanelLoginNavigationEnabled(true);
            UpdateQuickPanelNavigationButtonState(_quickPanelLoginNavButton, _trayMenuController.ActiveSection == TrayMenuSection.Login);
            bool settingsSectionActive = (_trayMenuController.ActiveSection is
                TrayMenuSection.Settings or
                TrayMenuSection.SettingsUsers or
                TrayMenuSection.SettingsServers or
                TrayMenuSection.Devices or
                TrayMenuSection.Accounts) ||
                _trayMenuController.ActiveSection == TrayMenuSection.Servers && _showInlineServerRegistration;
            UpdateQuickPanelNavigationButtonState(_quickPanelSettingsNavButton, settingsSectionActive);
            UpdateQuickPanelNavigationButtonState(_quickPanelCredentialsNavButton, _trayMenuController.ActiveSection == TrayMenuSection.Credentials);
            UpdateQuickPanelNavigationButtonState(_quickPanelFaqNavButton, _trayMenuController.ActiveSection == TrayMenuSection.Faq);
            LayoutQuickPanelMenuSections();
        }

        private void SetQuickPanelLoginNavigationEnabled(bool enabled)
        {
            _quickPanelLoginNavButton.Enabled = enabled;
            _quickPanelLoginNavButton.Cursor = enabled ? Cursors.Hand : Cursors.Default;
        }

        private void RefreshCredentialQuickPanelState()
        {
            if (!_isDeviceRegistered || _trayMenuController?.ActiveSection != TrayMenuSection.Credentials)
            {
                return;
            }

            GetActiveTraySelection(out DesktopRegistration? registration, out NamespaceItemDto? activeNamespace);

            string statusText = ResolveTrayStatus().StatusText;
            TrayMenuViewState viewState = BuildQuickPanelViewState(registration, activeNamespace, statusText);

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

        private static void UpdateQuickPanelNavigationButtonState(Button button, bool isActive)
        {
            if (!button.Enabled)
            {
                button.BackColor = Color.FromArgb(61, 63, 89);
                button.ForeColor = Color.FromArgb(141, 144, 176);
                button.FlatAppearance.BorderColor = Color.FromArgb(61, 63, 89);
                return;
            }

            button.BackColor = isActive ? Color.FromArgb(120, 205, 0) : Color.FromArgb(46, 48, 77);
            button.ForeColor = isActive ? Color.Black : Color.White;
            button.FlatAppearance.BorderColor = isActive ? Color.FromArgb(120, 205, 0) : Color.FromArgb(46, 48, 77);
        }

        private void LayoutQuickPanelMenuSections()
        {
            const int sectionGap = 15;
            const int labelToPanelGap = 24;

            ApplyPreferredStackPanelHeight(_quickPanelServerStatusPanel);
            ApplyPreferredStackPanelHeight(_quickPanelNavigationPanel);
            ApplyPreferredStackPanelHeight(_quickPanelActionPanel);

            _quickPanelMenuSectionLabel.Location = new Point(18, _quickPanelServerStatusPanel.Bottom + sectionGap);
            _quickPanelNavigationPanel.Location = new Point(18, _quickPanelMenuSectionLabel.Bottom + (labelToPanelGap - _quickPanelMenuSectionLabel.Height));
            _quickPanelActionSectionLabel.Location = new Point(18, _quickPanelNavigationPanel.Bottom + sectionGap);
            _quickPanelActionPanel.Location = new Point(18, _quickPanelActionSectionLabel.Bottom + (labelToPanelGap - _quickPanelActionSectionLabel.Height));
        }

        private void RefreshQuickPanelServerStatusList(DesktopRegistration? registration)
        {
            IReadOnlyList<TrayServerStatus> serverStatuses = BuildTrayServerStatuses(registration);
            if (_renderedServerStatuses.SequenceEqual(serverStatuses))
            {
                return;
            }

            _quickPanelServerStatusPanel.SuspendLayout();

            try
            {
                _quickPanelServerStatusPanel.Controls.Clear();

                foreach (TrayServerStatus serverStatus in serverStatuses)
                {
                    _quickPanelServerStatusPanel.Controls.Add(CreateQuickPanelServerStatusRow(
                        serverStatus.NamespaceId,
                        serverStatus.Name,
                        serverStatus.IsConnected,
                        serverStatus.IsActive));
                }

                _renderedServerStatuses = serverStatuses;
            }
            finally
            {
                _quickPanelServerStatusPanel.ResumeLayout(performLayout: true);
            }
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

        private Control CreateQuickPanelServerStatusRow(string namespaceId, string serverName, bool isConnected, bool isActive)
        {
            Panel rowPanel = new()
            {
                Size = new Size(140, 24),
                Margin = new Padding(0, 0, 0, 6),
                BackColor = Color.Transparent
            };

            Panel statusIndicator = new()
            {
                Size = new Size(10, 10),
                Location = new Point(0, 7),
                BackColor = isConnected ? Color.FromArgb(120, 205, 0) : Color.FromArgb(239, 68, 68)
            };

            Label serverNameLabel = new()
            {
                AutoSize = false,
                Location = new Point(18, 2),
                Size = new Size(94, 18),
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Text = serverName,
                AutoEllipsis = true
            };

            Label activeLabel = new()
            {
                AutoSize = false,
                Location = new Point(114, 2),
                Size = new Size(26, 18),
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 205, 0),
                Text = isActive ? "ACT" : string.Empty,
                TextAlign = ContentAlignment.MiddleRight
            };

            rowPanel.Controls.Add(statusIndicator);
            rowPanel.Controls.Add(serverNameLabel);
            rowPanel.Controls.Add(activeLabel);

            if (!string.IsNullOrWhiteSpace(namespaceId))
            {
                EventHandler selectServer = async (_, _) => await SelectActiveServerAsync(namespaceId);
                rowPanel.Cursor = Cursors.Hand;
                statusIndicator.Cursor = Cursors.Hand;
                serverNameLabel.Cursor = Cursors.Hand;
                activeLabel.Cursor = Cursors.Hand;
                rowPanel.Click += selectServer;
                statusIndicator.Click += selectServer;
                serverNameLabel.Click += selectServer;
                activeLabel.Click += selectServer;
            }

            return rowPanel;
        }

        private async Task SelectActiveServerAsync(string namespaceId)
        {
            if (_authenticationInProgress || string.IsNullOrWhiteSpace(namespaceId))
            {
                return;
            }

            AppEnvironment.SetActiveNamespace(namespaceId);
            _namespaceSelectionMenuController.RefreshMenu();
            HandleTopMenuNamespaceSelectionChanged();

            if (!_authenticationService.IsAuthorizedForActiveNamespace && _authenticationService.CanLoginForActiveNamespace)
            {
                loginToolStripMenuItem_Click(this, EventArgs.Empty);
                return;
            }

            await RefreshQuickPanelFromBackendAsync().ConfigureAwait(true);
        }

        private static void ApplyPreferredStackPanelHeight(FlowLayoutPanel panel)
        {
            panel.PerformLayout();
            int preferredHeight = panel.GetPreferredSize(new Size(panel.Width, 0)).Height;
            panel.Size = new Size(panel.Width, Math.Max(0, preferredHeight));
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

            return _trayMenuViews.Build(_trayMenuController?.ActiveSection ?? TrayMenuSection.Login, context);
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
            _quickPanelUserLabel.Text = credentialsLayout ? "Group" : "Active profile";
            _quickPanelNamespaceLabel.Text = credentialsLayout ? "Entries" : "Active server";
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
        }

        private void UpdateSettingsOverviewPresentation(TrayMenuViewState viewState)
        {
            TrayMenuSection? activeSection = _trayMenuController?.ActiveSection;
            bool settingsNavigationActive = activeSection is
                TrayMenuSection.Settings or
                TrayMenuSection.SettingsUsers or
                TrayMenuSection.SettingsServers or
                TrayMenuSection.Devices or
                TrayMenuSection.Accounts;
            bool settingsOverviewActive = activeSection == TrayMenuSection.Settings;

            _quickPanelSettingsOverviewPanel.Visible = settingsNavigationActive;
            _quickPanelSettingsDescriptionBox.Visible = settingsOverviewActive;
            _quickPanelDetailDescriptionLabel.Visible = !settingsOverviewActive;

            if (!settingsNavigationActive)
            {
                return;
            }

            UpdateSettingsOverviewLinkState(_quickPanelSettingsOverviewLink, TrayMenuSection.Settings);
            UpdateSettingsOverviewLinkState(_quickPanelSettingsProfileLink, TrayMenuSection.SettingsUsers);
            UpdateSettingsOverviewLinkState(_quickPanelSettingsServersLink, TrayMenuSection.SettingsServers);
            UpdateSettingsOverviewLinkState(_quickPanelSettingsDevicesLink, TrayMenuSection.Devices);

            if (settingsOverviewActive)
            {
                _quickPanelSelectionCard.Visible = false;
                _quickPanelMetricsCard.Visible = false;
                _quickPanelInsightCard.Visible = false;
                _quickPanelSettingsDescriptionBox.Text =
                    (string.IsNullOrWhiteSpace(_settingsAccessMessage)
                        ? string.Empty
                        : _settingsAccessMessage + "\n\n") +
                    $"{viewState.Description}\n\n" +
                    "Overview provides a summary of the available settings.\n\n" +
                    "Profile: manage the active profile and related actions.\n\n" +
                    "Servers: add, synchronize, and manage server connections.\n\n" +
                    "Devices: review and manage registered account devices.";
            }
            else if (activeSection is TrayMenuSection.SettingsUsers or TrayMenuSection.SettingsServers)
            {
                _quickPanelSelectionCard.Location = new Point(32, 205);
            }
            else if (activeSection == TrayMenuSection.Accounts)
            {
                ApplyGuidanceListLayout(_quickPanelAccountsGuidancePanel, _quickPanelAccountsPanel);
            }
            else if (activeSection == TrayMenuSection.Devices)
            {
                ApplyGuidanceListLayout(_quickPanelDevicesGuidancePanel, _quickPanelDevicesPanel);
                ApplyDevicesSourceFilterLayout();
            }

            LayoutSettingsOverview();
            _quickPanelSettingsOverviewPanel.BringToFront();
        }

        private void ApplyGuidanceListLayout(Panel guidancePanel, FlowLayoutPanel listPanel)
        {
            guidancePanel.Location = new Point(QuickPanelGuidanceLeft, QuickPanelGuidanceTop);
            listPanel.Location = new Point(QuickPanelGuidanceLeft, QuickPanelGuidanceListTop);
            listPanel.Height = _quickPanelDetailPanel.Height - listPanel.Top - QuickPanelContentBottomMargin;
        }

        private void UpdateSettingsOverviewLinkState(Label label, TrayMenuSection section)
        {
            bool available = !RequiresSettingsAuthentication(section) || _authenticationService.IsAuthorized;
            label.ForeColor = !available
                ? Color.FromArgb(119, 122, 151)
                : _trayMenuController?.ActiveSection == section
                    ? Color.FromArgb(120, 205, 0)
                    : Color.White;
            label.Cursor = available ? Cursors.Hand : Cursors.Default;
        }

        private void ApplyLoginProfileSelectionAvailability(TrayMenuViewState viewState)
        {
            if (viewState.SelectionLayout != TrayMenuSelectionLayout.LoginSelectors)
            {
                return;
            }

            bool profileSelectionLocked = _authenticationService.IsAuthorized ||
                _authenticationService.HasAuthorizedOtherProfile;
            _quickPanelUserComboBox.Enabled = !profileSelectionLocked && _quickPanelUserComboBox.Items.Count > 1;
            _quickPanelUserComboBox.Cursor = _quickPanelUserComboBox.Enabled ? Cursors.Hand : Cursors.Default;
        }

        private void UpdateAccountsPresentation()
        {
            bool accountsActive = _trayMenuController?.ActiveSection == TrayMenuSection.Accounts;
            ConfigureAccountsPresentationLayout(accountsActive);
            _quickPanelAccountsGuidancePanel.Visible = accountsActive;

            if (!accountsActive)
            {
                return;
            }

            PopulateAccountRows();
        }

        private void ConfigureAccountsPresentationLayout(bool accountsActive)
        {
            _quickPanelAccountsPanel.Visible = accountsActive;
            _quickPanelDetailDescriptionLabel.Size = accountsActive
                ? new Size(574, 60)
                : new Size(574, 44);
            _quickPanelAccountsPanel.Location = accountsActive
                ? new Point(32, 170)
                : new Point(32, 152);
            _quickPanelAccountsPanel.Size = accountsActive
                ? new Size(578, 372)
                : new Size(578, 390);

            if (!accountsActive)
            {
                return;
            }

            _quickPanelSelectionCard.Visible = false;
            _quickPanelMetricsCard.Visible = false;
            _quickPanelInsightCard.Visible = false;
        }

        private void ConfigureAccountsGuidanceBox()
        {
            _quickPanelAccountsGuidancePanel.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelAccountsGuidancePanel.BorderStyle = BorderStyle.FixedSingle;
            _quickPanelAccountsGuidancePanel.Location = new Point(32, 152);
            _quickPanelAccountsGuidancePanel.Size = new Size(578, 174);
            _quickPanelAccountsGuidancePanel.Visible = false;

            _quickPanelAccountsGuidanceBox.BackColor = _quickPanelAccountsGuidancePanel.BackColor;
            _quickPanelAccountsGuidanceBox.BorderStyle = BorderStyle.None;
            _quickPanelAccountsGuidanceBox.Cursor = Cursors.Default;
            _quickPanelAccountsGuidanceBox.DetectUrls = false;
            _quickPanelAccountsGuidanceBox.Font = _quickPanelDevicesGuidanceFont;
            _quickPanelAccountsGuidanceBox.ForeColor = Color.FromArgb(220, 222, 241);
            _quickPanelAccountsGuidanceBox.Location = new Point(8, 8);
            _quickPanelAccountsGuidanceBox.ReadOnly = true;
            _quickPanelAccountsGuidanceBox.ScrollBars = RichTextBoxScrollBars.None;
            _quickPanelAccountsGuidanceBox.ShortcutsEnabled = false;
            _quickPanelAccountsGuidanceBox.Size = new Size(560, 156);
            _quickPanelAccountsGuidanceBox.TabStop = false;
            _quickPanelAccountsGuidanceBox.Text = "Deleting an account completely deregisters all registered devices, credentials, and recovery data, including email addresses and phone numbers. All user-related information is permanently deleted and cannot be recovered.";
            _quickPanelAccountsGuidancePanel.Controls.Add(_quickPanelAccountsGuidanceBox);
        }

        private void PopulateAccountRows()
        {
            _quickPanelAccountsPanel.Controls.Clear();

            if (!_registeredDeviceService.TryReadRegistration(out DesktopRegistration? registration) || registration == null)
            {
                return;
            }

            foreach (NamespaceItemDto namespaceItem in registration.Namespaces)
            {
                _quickPanelAccountsPanel.Controls.Add(CreateAccountRow(registration.UserKey, namespaceItem));
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

            try
            {
                if (!_registeredDeviceService.TryReadRegistration(out DesktopRegistration? registration) || registration == null)
                {
                    _accountDeviceServerResults = Array.Empty<AccountDeviceServerResult>();
                    _visibleAccountDevices = Array.Empty<AccountDeviceListItem>();
                    _devicesLoadedOnce = true;
                    RebuildDeviceSourceSelector();
                    return;
                }

                List<NamespaceItemDto> authorizedNamespaces = registration.Namespaces
                    .Where(namespaceItem => _authorizationSessionStore.HasActiveSessionForNamespace(registration.UserKey, namespaceItem.NamespaceId))
                    .ToList();

                if (authorizedNamespaces.Count == 0)
                {
                    _accountDeviceServerResults = Array.Empty<AccountDeviceServerResult>();
                    _visibleAccountDevices = Array.Empty<AccountDeviceListItem>();
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
                _visibleAccountDevices = Array.Empty<AccountDeviceListItem>();
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
            if (_updatingDeviceSourceSelection || _quickPanelDevicesSourceComboBox.SelectedItem is not SelectionOption option)
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
                _quickPanelDevicesSourceComboBox.Items.Clear();
                _quickPanelDevicesSourceComboBox.Items.Add(new SelectionOption("All devices", DeviceSourceAllValue));

                foreach (string sourceUrl in _accountDeviceServerResults
                             .Select(result => result.SourceUrl)
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    _quickPanelDevicesSourceComboBox.Items.Add(new SelectionOption(sourceUrl, sourceUrl));
                }

                bool selectedValueExists = _quickPanelDevicesSourceComboBox.Items
                    .OfType<SelectionOption>()
                    .Any(option => string.Equals(option.Value, _selectedDeviceSourceValue, StringComparison.Ordinal));

                if (!selectedValueExists)
                {
                    _selectedDeviceSourceValue = DeviceSourceAllValue;
                }

                SelectComboBoxValue(_quickPanelDevicesSourceComboBox, _selectedDeviceSourceValue);
                _quickPanelDevicesSourceComboBox.Enabled = _quickPanelDevicesSourceComboBox.Items.Count > 1;
            }
            finally
            {
                _updatingDeviceSourceSelection = false;
            }
        }

        private void RebuildVisibleAccountDevices()
        {
            IEnumerable<AccountDeviceListItem> mappedItems = _accountDeviceServerResults
                .Where(result => string.IsNullOrWhiteSpace(result.ErrorMessage))
                .SelectMany(result => result.Devices.Select(device => new AccountDeviceListItem
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
            _visibleAccountDevices = Array.Empty<AccountDeviceListItem>();
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
            bool devicesActive = _trayMenuController?.ActiveSection == TrayMenuSection.Devices;
            _quickPanelDevicesPanel.Visible = devicesActive;
            _quickPanelDevicesGuidancePanel.Visible = devicesActive;
            _quickPanelDevicesSourceLabel.Visible = devicesActive;
            _quickPanelDevicesSourceComboBox.Visible = devicesActive;

            if (!devicesActive)
            {
                _quickPanelDetailTitleLabel.AutoSize = true;
                _quickPanelDetailTitleLabel.Font = _quickPanelDetailDefaultTitleFont;
                _quickPanelDevicesPanel.Location = new Point(32, 152);
                _quickPanelDevicesPanel.Size = new Size(578, 390);
                return;
            }

            _quickPanelDetailTitleLabel.AutoSize = false;
            _quickPanelDetailTitleLabel.Font = _quickPanelDevicesTitleFont;
            _quickPanelDetailTitleLabel.Size = new Size(550, 40);
            _quickPanelDetailDescriptionLabel.Size = new Size(574, 44);
            ApplyGuidanceListLayout(_quickPanelDevicesGuidancePanel, _quickPanelDevicesPanel);
            ApplyDevicesSourceFilterLayout();
            _quickPanelDevicesGuidancePanel.BringToFront();
            _quickPanelDevicesSourceLabel.BringToFront();
            _quickPanelDevicesSourceComboBox.BringToFront();
            _quickPanelSelectionCard.Visible = false;
            _quickPanelMetricsCard.Visible = false;
            _quickPanelInsightCard.Visible = false;
            _quickPanelDevicesPanel.Controls.Clear();

            if (_devicesLoading)
            {
                _quickPanelDevicesPanel.Controls.Add(CreateDeviceListMessage("Loading registered devices..."));
                return;
            }

            if (!_devicesLoadedOnce)
            {
                _quickPanelDevicesPanel.Controls.Add(CreateDeviceListMessage("Select Devices to load registered devices from all logged-in servers."));
                return;
            }

            if (_accountDeviceServerResults.Count == 0)
            {
                _quickPanelDevicesPanel.Controls.Add(CreateDeviceListMessage("Log in to one or more servers to manage devices."));
                return;
            }

            if (!string.IsNullOrWhiteSpace(_devicesErrorMessage))
            {
                _quickPanelDevicesPanel.Controls.Add(CreateDeviceListMessage(_devicesErrorMessage));
            }

            if (_visibleAccountDevices.Count == 0)
            {
                _quickPanelDevicesPanel.Controls.Add(CreateDeviceListMessage("No registered devices were found for the selected server scope."));
                return;
            }

            foreach (AccountDeviceListItem device in _visibleAccountDevices)
            {
                _quickPanelDevicesPanel.Controls.Add(CreateDeviceRow(device));
            }
        }

        private void ConfigureDevicesGuidanceBox()
        {
            const string guidance = "1. Replace your mobile device:\n" +
                "Install the mobile app on the new device, enter your server URL(s), and follow the instructions on the phone.\n\n" +
                "2. Delete a device:\n" +
                "You can delete any mobile or desktop device from any other registered device.\n\n" +
                "3. Keep at least one registered device:\n" +
                "Keep one mobile or desktop device registered to your servers so you can easily add devices to your account.\n\n" +
                "4. Recovery:\n" +
                "If all mobile and desktop devices are deleted from your account, follow the Recovery description.";

            _quickPanelDevicesGuidancePanel.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelDevicesGuidancePanel.BorderStyle = BorderStyle.FixedSingle;
            _quickPanelDevicesGuidancePanel.Location = new Point(32, 152);
            _quickPanelDevicesGuidancePanel.Size = new Size(578, 174);
            _quickPanelDevicesGuidancePanel.Visible = false;

            _quickPanelDevicesGuidanceBox.BackColor = _quickPanelDevicesGuidancePanel.BackColor;
            _quickPanelDevicesGuidanceBox.BorderStyle = BorderStyle.None;
            _quickPanelDevicesGuidanceBox.Cursor = Cursors.Default;
            _quickPanelDevicesGuidanceBox.DetectUrls = false;
            _quickPanelDevicesGuidanceBox.Font = _quickPanelDevicesGuidanceFont;
            _quickPanelDevicesGuidanceBox.ForeColor = Color.FromArgb(220, 222, 241);
            _quickPanelDevicesGuidanceBox.Location = new Point(8, 8);
            _quickPanelDevicesGuidanceBox.ReadOnly = true;
            _quickPanelDevicesGuidanceBox.ScrollBars = RichTextBoxScrollBars.None;
            _quickPanelDevicesGuidanceBox.ShortcutsEnabled = false;
            _quickPanelDevicesGuidanceBox.Size = new Size(560, 156);
            _quickPanelDevicesGuidanceBox.TabStop = false;
            _quickPanelDevicesGuidanceBox.Text = guidance;
            _quickPanelDevicesGuidancePanel.Controls.Add(_quickPanelDevicesGuidanceBox);

            EmphasizeDevicesGuidance("1. Replace your mobile device:");
            EmphasizeDevicesGuidance("2. Delete a device:");
            EmphasizeDevicesGuidance("3. Keep at least one registered device:");
            EmphasizeDevicesGuidance("4. Recovery:");
            EmphasizeDevicesGuidance("Keep one mobile or desktop device registered");
            EmphasizeDevicesGuidance("Recovery description");
            _quickPanelDevicesGuidanceBox.Select(0, 0);
        }

        private void EmphasizeDevicesGuidance(string text)
        {
            int startIndex = _quickPanelDevicesGuidanceBox.Text.IndexOf(text, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return;
            }

            _quickPanelDevicesGuidanceBox.Select(startIndex, text.Length);
            _quickPanelDevicesGuidanceBox.SelectionFont = _quickPanelDevicesGuidanceBoldFont;
        }

        private static Label CreateDeviceListMessage(string message)
        {
            return new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(171, 174, 211),
                Margin = new Padding(0),
                Size = new Size(550, 42),
                Text = message,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private Control CreateDeviceRow(AccountDeviceListItem item)
        {
            AccountDeviceDto device = item.Device;
            Panel row = new()
            {
                BackColor = Color.FromArgb(46, 48, 77),
                Margin = new Padding(0, 0, 0, 8),
                Size = new Size(550, 76)
            };
            string deviceIdentifier = !string.IsNullOrWhiteSpace(device.DesktopDeviceId)
                ? device.DesktopDeviceId
                : device.MobileDeviceId ?? "Unknown device";
            string deviceName = string.IsNullOrWhiteSpace(device.DeviceName)
                ? deviceIdentifier
                : device.DeviceName;
            string lastUsed = device.LastTimeUsed.HasValue
                ? device.LastTimeUsed.Value.LocalDateTime.ToString("g")
                : "Unknown";
            string namespaceSummary = device.Namespaces.Count == 0
                ? "No linked servers"
                : string.Join(", ", device.Namespaces.Select(item => item.Namespace));

            Label nameLabel = new()
            {
                AutoEllipsis = true,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(12, 10),
                Size = new Size(350, 18),
                Text = deviceName
            };
            Label detailLabel = new()
            {
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(171, 174, 211),
                Location = new Point(12, 32),
                Size = new Size(350, 18),
                Text = $"{deviceIdentifier} | Last used: {lastUsed}"
            };
            Label namespaceLabel = new()
            {
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(171, 174, 211),
                Location = new Point(12, 52),
                Size = new Size(350, 18),
                Text = $"{namespaceSummary} | Source: {item.SourceUrl}"
            };
            Panel actionPanel = new()
            {
                BackColor = row.BackColor,
                Location = new Point(374, 18),
                Size = new Size(154, 40)
            };
            string tooltip = $"{deviceIdentifier}{Environment.NewLine}{namespaceSummary}{Environment.NewLine}Source: {item.SourceUrl}";
            _quickPanelToolTip.SetToolTip(row, tooltip);
            _quickPanelToolTip.SetToolTip(nameLabel, tooltip);
            _quickPanelToolTip.SetToolTip(detailLabel, tooltip);
            _quickPanelToolTip.SetToolTip(namespaceLabel, tooltip);

            row.Controls.Add(nameLabel);
            row.Controls.Add(detailLabel);
            row.Controls.Add(namespaceLabel);
            row.Controls.Add(actionPanel);

            if (string.Equals(_devicePendingDeletionKey, item.IdentityKey, StringComparison.Ordinal))
            {
                AddDeviceDeletionConfirmation(actionPanel, item);
            }
            else
            {
                Button deleteButton = new()
                {
                    AccessibleName = "Delete device",
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.FromArgb(214, 88, 88),
                    Location = new Point(108, 0),
                    Size = new Size(46, 40),
                    UseVisualStyleBackColor = false
                };
                deleteButton.FlatAppearance.BorderSize = 0;
                deleteButton.Paint += DrawDeleteAccountIcon;
                deleteButton.Click += (_, _) =>
                {
                    _devicePendingDeletionKey = item.IdentityKey;
                    UpdateDevicesPresentation();
                };
                actionPanel.Controls.Add(deleteButton);
            }

            return row;
        }

        private void AddDeviceDeletionConfirmation(Panel actionPanel, AccountDeviceListItem item)
        {
            Button cancelButton = new()
            {
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Location = new Point(0, 5),
                Size = new Size(68, 30),
                Text = "Cancel",
                UseVisualStyleBackColor = false
            };
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(100, 104, 143);
            cancelButton.Click += (_, _) =>
            {
                _devicePendingDeletionKey = string.Empty;
                UpdateQuickPanelState();
            };

            Button confirmButton = new()
            {
                Enabled = !_deviceDeletionInProgress,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(214, 88, 88),
                ForeColor = Color.White,
                Location = new Point(76, 5),
                Size = new Size(76, 30),
                Text = _deviceDeletionInProgress ? "Deleting" : "Delete",
                UseVisualStyleBackColor = false
            };
            confirmButton.FlatAppearance.BorderSize = 0;
            confirmButton.Click += async (_, _) => await DeleteDeviceFromQuickPanelAsync(item);

            actionPanel.Controls.Add(cancelButton);
            actionPanel.Controls.Add(confirmButton);
        }

        private async Task DeleteDeviceFromQuickPanelAsync(AccountDeviceListItem item)
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

                _userSelectionMenuController.RefreshMenu();
                _namespaceSelectionMenuController.RefreshMenu();
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

        private Control CreateAccountRow(string userId, NamespaceItemDto namespaceItem)
        {
            Panel row = new()
            {
                BackColor = Color.FromArgb(46, 48, 77),
                Margin = new Padding(0, 0, 0, 8),
                Size = new Size(550, 64)
            };
            Label urlLabel = new()
            {
                AutoEllipsis = true,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(12, 10),
                Size = new Size(455, 18),
                Text = namespaceItem.Url
            };
            Label accountLabel = new()
            {
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(171, 174, 211),
                Location = new Point(12, 34),
                Size = new Size(455, 18),
                Text = namespaceItem.AccountId
            };
            Button deleteButton = new()
            {
                AccessibleName = "Delete account",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(214, 88, 88),
                Location = new Point(486, 14),
                Size = new Size(42, 36),
                UseVisualStyleBackColor = false
            };
            deleteButton.FlatAppearance.BorderSize = 0;
            deleteButton.Paint += DrawDeleteAccountIcon;
            deleteButton.Click += async (_, _) => await DeleteAccountFromQuickPanelAsync(userId, namespaceItem);
            row.Controls.Add(urlLabel);
            row.Controls.Add(accountLabel);
            row.Controls.Add(deleteButton);
            return row;
        }

        private static void DrawDeleteAccountIcon(object? sender, PaintEventArgs eventArgs)
        {
            if (sender is not Button button)
            {
                return;
            }

            using var pen = new Pen(button.ForeColor, 2F);
            Rectangle bounds = button.ClientRectangle;
            int centerX = bounds.Width / 2;
            eventArgs.Graphics.DrawRectangle(pen, centerX - 6, 13, 12, 13);
            eventArgs.Graphics.DrawLine(pen, centerX - 8, 10, centerX + 8, 10);
            eventArgs.Graphics.DrawLine(pen, centerX - 2, 7, centerX + 2, 7);
            eventArgs.Graphics.DrawLine(pen, centerX - 2, 16, centerX - 2, 23);
            eventArgs.Graphics.DrawLine(pen, centerX + 2, 16, centerX + 2, 23);
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
            bool settingsUsersSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.SettingsUsers;
            bool settingsServersSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.SettingsServers;
            bool settingsActionSectionActive = settingsUsersSectionActive || settingsServersSectionActive;
            bool serversSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.Servers;
            bool credentialsSectionActive = viewState.SelectionLayout == TrayMenuSelectionLayout.CredentialSelectors;
            bool showInlineRegistration = serversSectionActive && _showInlineServerRegistration;
            bool isNewServerRegistration = _inlineServerRegistrationMode == InlineServerRegistrationMode.NewServer;
            _quickPanelServerRegistrationGuidancePanel.Visible = showInlineRegistration;
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
            _quickPanelUserRegistrationPanel.Visible = showInlineUserRegistration;
            _quickPanelUserDeletionPanel.Visible = showInlineUserDeletion;
            if (showInlineUserRegistration)
            {
                _quickPanelUserRegistrationPanel.BringToFront();
            }
            else if (showInlineUserDeletion)
            {
                _quickPanelUserDeletionPanel.BringToFront();
                _quickPanelDetailTitleLabel.Text = "Delete user profile";
                _quickPanelDetailDescriptionLabel.Text = string.Empty;
            }

            _quickPanelRegisterNewServerInlineButton.Visible = serversSectionActive && !showInlineRegistration;
            _quickPanelRegisterNewServerInlineButton.Enabled = canStartRegistration;
            _quickPanelServerRegistrationInlineButton.Visible = serversSectionActive;
            _quickPanelServerRegistrationInlineButton.Enabled = !_inlineServerRegistrationAwaitingCompletion && !_inlineServerRegistrationSubmitting && canStartRegistration;
            _quickPanelServerStatusValueLabel.Visible = serversSectionActive && !showInlineRegistration;
            _quickPanelServerLoginInlineButton.Visible = (loginSectionActive || serversSectionActive) && !showInlineRegistration;
            _quickPanelServerLoginInlineButton.Enabled = canLoginToActiveNamespace || canLogoutFromActiveNamespace;
            _quickPanelServerLoginInlineButton.Text = _authenticationInProgress
                ? isAuthorizedForActiveNamespace ? "Logging out..." : "Logging in..."
                : isAuthorizedForActiveNamespace ? "Logout" : "Login";
            if (loginSectionActive && loginBlockedByAnotherProfile)
            {
                _quickPanelDetailDescriptionLabel.Text = "Only one profile can be signed in at a time. Log out before signing in with another profile.";
            }
            _quickPanelSelectionCard.Visible = !credentialGroupManagerActive;
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
                SetServerRegistrationControlsVisible(false);
                return;
            }

            if (!serversSectionActive)
            {
                if (loginSectionActive)
                {
                    bool showUserSelector = viewState.ShowUserSelector;
                    _quickPanelSelectionCard.Size = new Size(578, showUserSelector ? 166 : 108);
                    _quickPanelServerLoginInlineButton.Location = new Point(438, showUserSelector ? 132 : 72);
                    _quickPanelServerLoginInlineButton.Size = new Size(122, 28);
                    _quickPanelServerLoginInlineButton.Visible = true;
                    _quickPanelInsightCard.Visible = false;
                    SetServerRegistrationControlsVisible(false);
                    return;
                }

                if (settingsActionSectionActive)
                {
                    if (settingsUsersSectionActive && (showInlineUserRegistration || showInlineUserDeletion))
                    {
                        _quickPanelSelectionCard.Visible = false;
                    }

                    _quickPanelInsightCard.Visible = false;
                    SetServerRegistrationControlsVisible(false);
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
                SetServerRegistrationControlsVisible(false);
                return;
            }

            if (!showInlineRegistration)
            {
                _quickPanelSelectionCard.Size = new Size(578, 108);
                _quickPanelNamespaceLabel.Location = new Point(16, 50);
                _quickPanelNamespaceComboBox.Location = new Point(16, 72);
                _quickPanelNamespaceComboBox.Size = new Size(544, 25);
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
                SetServerRegistrationControlsVisible(false);
                return;
            }

            _quickPanelSelectionCard.Visible = false;
            _quickPanelMetricsCard.Visible = false;
            _quickPanelDetailTitleLabel.Text = isNewServerRegistration ? "Add a server" : "Sync existing server";
            UpdateServerRegistrationGuidance(isNewServerRegistration);
            _quickPanelInsightCard.Location = new Point(32, 152);
            _quickPanelInsightCard.Size = new Size(578, 493);
            _quickPanelInsightTitleLabel.Location = new Point(92, 17);
            _quickPanelInsightTitleLabel.Text = string.Empty;
            _quickPanelInsightBodyLabel.Visible = false;
            _quickPanelRegisterNewServerInlineButton.Visible = false;
            _quickPanelServerRegistrationInlineButton.Visible = false;
            int formTop = 174;
            int actionTop = 232;
            _quickPanelServerRegistrationUrlLabel.Location = new Point(16, formTop);
            _quickPanelServerRegistrationUrlTextBox.Location = new Point(16, formTop + 20);
            _quickPanelServerRegistrationUrlTextBox.Size = isNewServerRegistration
                ? new Size(260, 25)
                : new Size(544, 25);
            _quickPanelServerRegistrationNameLabel.Location = new Point(300, formTop);
            _quickPanelServerRegistrationNameTextBox.Location = new Point(300, formTop + 20);
            _quickPanelServerRegistrationNameTextBox.Size = new Size(260, 25);
            SetServerRegistrationControlsVisible(true, isNewServerRegistration);
            _quickPanelServerRegistrationBackButton.Location = new Point(16, actionTop);
            _quickPanelServerRegistrationSubmitButton.Location = new Point(
                _quickPanelServerRegistrationBackButton.Right + 5,
                _quickPanelServerRegistrationBackButton.Top);
            _quickPanelServerRegistrationStatusLabel.Location = new Point(176, 236);
            _quickPanelServerRegistrationQrPictureBox.Location = new Point(16, 270);
            _quickPanelServerRegistrationStatusLabel.Text = _serverRegistrationStatusMessage;
            _quickPanelServerRegistrationSubmitButton.Enabled = !_inlineServerRegistrationSubmitting && !_inlineServerRegistrationAwaitingCompletion;
            _quickPanelServerRegistrationSubmitButton.Text = _inlineServerRegistrationSubmitting
                ? "Generating..."
                : _inlineServerRegistrationAwaitingCompletion
                    ? "QR ready"
                    : "Continue";
            _quickPanelServerRegistrationQrPictureBox.Visible = _quickPanelServerRegistrationQrPictureBox.Image != null;
            _quickPanelServerRegistrationBackButton.Visible = true;
        }

        private void ConfigureServerRegistrationGuidanceBox()
        {
            _quickPanelServerRegistrationGuidancePanel.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelServerRegistrationGuidancePanel.BorderStyle = BorderStyle.FixedSingle;
            _quickPanelServerRegistrationGuidancePanel.Location = new Point(16, 50);
            _quickPanelServerRegistrationGuidancePanel.Size = new Size(546, 110);
            _quickPanelServerRegistrationGuidancePanel.Visible = false;

            _quickPanelServerRegistrationGuidanceBox.BackColor = _quickPanelServerRegistrationGuidancePanel.BackColor;
            _quickPanelServerRegistrationGuidanceBox.BorderStyle = BorderStyle.None;
            _quickPanelServerRegistrationGuidanceBox.Cursor = Cursors.Default;
            _quickPanelServerRegistrationGuidanceBox.DetectUrls = false;
            _quickPanelServerRegistrationGuidanceBox.Font = _quickPanelDevicesGuidanceFont;
            _quickPanelServerRegistrationGuidanceBox.ForeColor = Color.FromArgb(220, 222, 241);
            _quickPanelServerRegistrationGuidanceBox.Location = new Point(8, 8);
            _quickPanelServerRegistrationGuidanceBox.ReadOnly = true;
            _quickPanelServerRegistrationGuidanceBox.ScrollBars = RichTextBoxScrollBars.None;
            _quickPanelServerRegistrationGuidanceBox.ShortcutsEnabled = false;
            _quickPanelServerRegistrationGuidanceBox.Size = new Size(528, 92);
            _quickPanelServerRegistrationGuidanceBox.TabStop = false;
            _quickPanelServerRegistrationGuidancePanel.Controls.Add(_quickPanelServerRegistrationGuidanceBox);

            UpdateServerRegistrationGuidance(isNewServerRegistration: true);
        }

        private void UpdateServerRegistrationGuidance(bool isNewServerRegistration)
        {
            string guidance = isNewServerRegistration
                ? "Add a server:\n" +
                    "Enter the new server URL and name, then select Continue.\n\n" +
                    "Scan the QR code with a mobile device already registered to your account. The mobile app registers this desktop with the new server; no separate mobile registration is needed."
                : "Sync existing server:\n" +
                    "Use this when your mobile device is already registered on the target server.\n\n" +
                    "Select Continue, then scan the QR code in the mobile app to connect this desktop.";

            if (string.Equals(_quickPanelServerRegistrationGuidanceBox.Text, guidance, StringComparison.Ordinal))
            {
                return;
            }

            _quickPanelServerRegistrationGuidanceBox.Text = guidance;

            EmphasizeServerRegistrationGuidance(isNewServerRegistration ? "Add a server:" : "Sync existing server:");
            EmphasizeServerRegistrationGuidance(isNewServerRegistration
                ? "no separate mobile registration is needed."
                : "already registered on the target server.");
            EmphasizeServerRegistrationGuidance(isNewServerRegistration
                ? "Scan the QR code"
                : "scan the QR code");
            _quickPanelServerRegistrationGuidanceBox.Select(0, 0);
        }

        private void EmphasizeServerRegistrationGuidance(string text)
        {
            int startIndex = _quickPanelServerRegistrationGuidanceBox.Text.IndexOf(text, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return;
            }

            _quickPanelServerRegistrationGuidanceBox.Select(startIndex, text.Length);
            _quickPanelServerRegistrationGuidanceBox.SelectionFont = _quickPanelDevicesGuidanceBoldFont;
        }

        private void SetServerRegistrationControlsVisible(bool visible, bool showNamespaceName = false)
        {
            _quickPanelServerRegistrationUrlLabel.Visible = visible;
            _quickPanelServerRegistrationUrlTextBox.Visible = visible;
            _quickPanelServerRegistrationNameLabel.Visible = visible && showNamespaceName;
            _quickPanelServerRegistrationNameTextBox.Visible = visible && showNamespaceName;
            _quickPanelServerRegistrationSubmitButton.Visible = visible;
            _quickPanelServerRegistrationBackButton.Visible = visible;
            _quickPanelServerRegistrationStatusLabel.Visible = visible;
            _quickPanelServerRegistrationQrPictureBox.Visible = visible && _quickPanelServerRegistrationQrPictureBox.Image != null;
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
            _quickPanelServerRegistrationQrPictureBox.Image = null;

            if (clearInputs)
            {
                _quickPanelServerRegistrationUrlTextBox.Text = string.Empty;
                _quickPanelServerRegistrationNameTextBox.Text = string.Empty;
            }
        }

        private void ConfigureSelectionCardLayout(TrayMenuViewState viewState)
        {
            TrayMenuSelectionLayout selectionLayout = viewState.SelectionLayout;
            bool loginSelectors = selectionLayout == TrayMenuSelectionLayout.LoginSelectors;
            bool settingsUserActions = selectionLayout == TrayMenuSelectionLayout.SettingsUserActions;
            bool settingsServerActions = selectionLayout == TrayMenuSelectionLayout.SettingsServerActions;
            bool showUserSelector = selectionLayout is TrayMenuSelectionLayout.UserSelector or TrayMenuSelectionLayout.UserAndServerSelectors or TrayMenuSelectionLayout.CredentialSelectors ||
                loginSelectors && viewState.ShowUserSelector;
            bool showServerSelector = selectionLayout is TrayMenuSelectionLayout.ServerSelector or TrayMenuSelectionLayout.UserAndServerSelectors or TrayMenuSelectionLayout.CredentialSelectors;
            showServerSelector |= loginSelectors;
            bool showHomeSummary = selectionLayout == TrayMenuSelectionLayout.HomeSummary;

            _quickPanelUserLabel.Visible = showUserSelector;
            _quickPanelUserComboBox.Visible = showUserSelector;
            _quickPanelNamespaceLabel.Visible = showServerSelector;
            _quickPanelNamespaceComboBox.Visible = showServerSelector;
            _quickPanelHomeActiveUserLabel.Visible = showHomeSummary;
            _quickPanelHomeActiveServerLabel.Visible = showHomeSummary;
            _quickPanelHomeSessionLabel.Visible = showHomeSummary;
            _quickPanelCredentialGroupSettingsButton.Visible = selectionLayout == TrayMenuSelectionLayout.CredentialSelectors;
            _quickPanelCredentialEntryCreateIconButton.Visible = selectionLayout == TrayMenuSelectionLayout.CredentialSelectors;
            _quickPanelSettingsUserActionLabel.Visible = settingsUserActions;
            _quickPanelSettingsUserActionComboBox.Visible = settingsUserActions;
            _quickPanelSettingsServerActionLabel.Visible = settingsServerActions;
            _quickPanelSettingsServerActionComboBox.Visible = settingsServerActions;
            _quickPanelRegisterNewServerInlineButton.Visible = false;
            _quickPanelServerStatusValueLabel.Visible = false;
            _quickPanelServerLoginInlineButton.Visible = false;

            if (selectionLayout == TrayMenuSelectionLayout.HomeSummary)
            {
                _quickPanelSelectionCard.Size = new Size(578, 106);
                _quickPanelHomeActiveUserLabel.Location = new Point(16, 16);
                _quickPanelHomeActiveServerLabel.Location = new Point(16, 44);
                _quickPanelHomeSessionLabel.Location = new Point(16, 72);
                return;
            }

            if (settingsUserActions)
            {
                _quickPanelSelectionCard.Location = new Point(32, 152);
                _quickPanelSelectionCard.Size = new Size(578, 74);
                _quickPanelSettingsUserActionLabel.Location = new Point(16, 14);
                _quickPanelSettingsUserActionComboBox.Location = new Point(16, 36);
                _quickPanelSettingsUserActionComboBox.Size = new Size(544, 25);
                return;
            }

            if (settingsServerActions)
            {
                _quickPanelSelectionCard.Location = new Point(32, 152);
                _quickPanelSelectionCard.Size = new Size(578, 74);
                _quickPanelSettingsServerActionLabel.Location = new Point(16, 14);
                _quickPanelSettingsServerActionComboBox.Location = new Point(16, 36);
                _quickPanelSettingsServerActionComboBox.Size = new Size(544, 25);
                return;
            }

            if (selectionLayout is TrayMenuSelectionLayout.UserAndServerSelectors or TrayMenuSelectionLayout.CredentialSelectors || loginSelectors && showUserSelector)
            {
                if (selectionLayout == TrayMenuSelectionLayout.CredentialSelectors)
                {
                    _quickPanelSelectionCard.Location = new Point(32, 137);
                    _quickPanelSelectionCard.Size = new Size(578, 74);
                    _quickPanelUserLabel.Location = new Point(16, 14);
                    _quickPanelUserComboBox.Location = new Point(16, 36);
                    _quickPanelUserComboBox.Size = new Size(244, 25);
                    _quickPanelCredentialGroupSettingsButton.Location = new Point(130, 10);
                    _quickPanelNamespaceLabel.Location = new Point(308, 14);
                    _quickPanelNamespaceComboBox.Location = new Point(308, 36);
                    _quickPanelNamespaceComboBox.Size = new Size(216, 25);
                    _quickPanelCredentialEntryCreateIconButton.Location = new Point(374, 10);
                    return;
                }

                _quickPanelSelectionCard.Size = new Size(578, 126);
                _quickPanelSelectionCard.Location = new Point(32, 152);
                _quickPanelUserLabel.Location = new Point(16, 14);
                _quickPanelUserComboBox.Location = new Point(16, 36);
                _quickPanelUserComboBox.Size = new Size(544, 25);
                _quickPanelNamespaceLabel.Location = new Point(16, 72);
                _quickPanelNamespaceComboBox.Location = new Point(16, 92);
                _quickPanelNamespaceComboBox.Size = new Size(544, 25);
                return;
            }

            _quickPanelSelectionCard.Size = new Size(578, 74);
            _quickPanelSelectionCard.Location = new Point(32, 152);

            if (showUserSelector)
            {
                _quickPanelUserLabel.Location = new Point(16, 14);
                _quickPanelUserComboBox.Location = new Point(16, 42);
                _quickPanelUserComboBox.Size = new Size(544, 25);
            }

            if (showServerSelector)
            {
                _quickPanelNamespaceLabel.Location = new Point(16, 14);
                _quickPanelNamespaceComboBox.Location = new Point(16, 36);
                _quickPanelNamespaceComboBox.Size = new Size(544, 25);
            }
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
            _quickPanelCredentialWorkspacePanel.Visible = credentialsLayout;
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
                _quickPanelCredentialEntryEditorPanel.Visible = showEntryEditor;
                _quickPanelCredentialGroupEditorPanel.Visible = showGroupManager;
                _quickPanelCredentialBackButton.Visible = (showGroupManager || showEntryEditor) &&
                    !showCredentialDeletionConfirmation && !showGroupDeletionConfirmation;
                _quickPanelCredentialSaveButton.Left = showCredentialDeletionConfirmation ? 0 : canDeleteCredential ? 112 : 0;
                _quickPanelCredentialDeleteButton.Left = showCredentialDeletionConfirmation ? 112 : 0;
                _quickPanelCredentialGroupSaveButton.Location = new Point(
                    showGroupDeletionConfirmation || showCreateGroup ? 0 : 158,
                    122);
                _quickPanelCredentialGroupDeleteButton.Location = new Point(showGroupDeletionConfirmation ? 112 : 0, 122);
                LayoutCredentialEntryEditor();
                _quickPanelCredentialBackButton.Location = showGroupManager
                    ? new Point(
                        _quickPanelCredentialGroupEditorPanel.Left + _quickPanelCredentialGroupSaveButton.Right + 8,
                        _quickPanelCredentialGroupEditorPanel.Top + _quickPanelCredentialGroupSaveButton.Top)
                    : new Point(
                        _quickPanelCredentialEntryEditorPanel.Left + _quickPanelCredentialSaveButton.Right + 8,
                        _quickPanelCredentialEntryEditorPanel.Top + _quickPanelCredentialSaveButton.Top);

                if (showGroupManager)
                {
                    _quickPanelCredentialGroupEditorPanel.BringToFront();
                    _quickPanelCredentialBackButton.BringToFront();
                }
                else
                {
                    _quickPanelCredentialEntryEditorPanel.BringToFront();

                    if (showEntryEditor)
                    {
                        _quickPanelCredentialBackButton.BringToFront();
                    }
                }

                _quickPanelCredentialSaveButton.Text = showCredentialDeletionConfirmation
                    ? "Cancel"
                    : showCreateEntry ? "Save new item" : "Save changes";
                _quickPanelCredentialStatusLabel.Text = _credentialEditorStatusMessage;
                _quickPanelGroupStatusLabel.Text = _groupEditorStatusMessage;

                _quickPanelCredentialDeleteButton.Visible = canDeleteCredential;
                _quickPanelCredentialDeleteButton.Enabled = canDeleteCredential && !_credentialsLoading;
                _quickPanelCredentialDeleteButton.Text = showCredentialDeletionConfirmation ? "Delete" : "Delete entry";
                _quickPanelCredentialEditorModeLabel.Text = showCreateEntry
                    ? "Create credential mode"
                    : "Read / Edit / Delete mode";
                _quickPanelCredentialEditorModeLabel.Visible = showCreateEntry || showCredentialReadEditDeleteMode;

                _quickPanelManagedGroupLabel.Visible = !showCreateGroup;
                _quickPanelManagedGroupComboBox.Visible = !showCreateGroup;
                _quickPanelManagedGroupNameLabel.Visible = !showCreateGroup;
                _quickPanelManagedGroupNameTextBox.Visible = !showCreateGroup;
                _quickPanelCredentialGroupCreateButton.Visible = false;
                _quickPanelCredentialGroupDeleteButton.Visible = !showCreateGroup;
                _quickPanelCredentialGroupDeleteButton.Enabled = canDeleteGroup && !_credentialsLoading;
                _quickPanelCredentialGroupDeleteButton.Text = showGroupDeletionConfirmation ? "Delete" : "Delete selected group";
                _quickPanelCredentialGroupSaveButton.Text = showGroupDeletionConfirmation
                    ? "Cancel"
                    : showCreateGroup ? "Save group" : "Save changes";
                _quickPanelGroupDeletionInfoPanel.Visible = !showCreateGroup && !showGroupDeletionConfirmation;
                _quickPanelNewGroupNameLabel.Visible = showCreateGroup;
                _quickPanelNewGroupNameTextBox.Visible = showCreateGroup;

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

            if (!showGroupManager)
            {
                bool showSelectorCard = !showCreateEntry;
                _quickPanelSelectionCard.Visible = showSelectorCard;
                _quickPanelCredentialWorkspacePanel.Location = new Point(32, showSelectorCard ? 226 : 137);
                MoveCredentialGroupActionButton(_quickPanelSelectionCard);
                _quickPanelUserLabel.Visible = showSelectorCard;
                _quickPanelUserComboBox.Visible = showSelectorCard;
                _quickPanelUserComboBox.Location = new Point(16, 36);
                _quickPanelUserComboBox.Size = new Size(244, 25);
                _quickPanelCredentialGroupSettingsButton.Visible = showSelectorCard;
                _quickPanelCredentialGroupSettingsButton.Location = new Point(130, 10);
                _quickPanelCredentialGroupSettingsButton.Text = "⚙ Manage groups";
                _quickPanelCredentialGroupSettingsButton.BackColor = Color.FromArgb(46, 48, 77);
                _quickPanelCredentialGroupSettingsButton.ForeColor = Color.FromArgb(120, 205, 0);
                _quickPanelNamespaceLabel.Visible = showSelectorCard;
                _quickPanelNamespaceComboBox.Visible = showSelectorCard;
                _quickPanelNamespaceLabel.Location = new Point(308, 14);
                _quickPanelNamespaceComboBox.Location = new Point(308, 36);
                _quickPanelNamespaceComboBox.Size = new Size(216, 25);
                _quickPanelCredentialEntryCreateIconButton.Visible = showSelectorCard;
                _quickPanelCredentialEntryCreateIconButton.Location = new Point(374, 10);
                _quickPanelCredentialEntryCreateIconButton.Text = "+ Create new entry";
                _quickPanelCredentialEntryCreateIconButton.BackColor = Color.FromArgb(46, 48, 77);
                _quickPanelCredentialEntryCreateIconButton.ForeColor = Color.FromArgb(120, 205, 0);
                _quickPanelCredentialEntryCreateIconButton.Enabled = true;
                return;
            }

            _quickPanelSelectionCard.Visible = false;
            _quickPanelCredentialWorkspacePanel.Location = new Point(32, 137);
            MoveCredentialGroupActionButton(_quickPanelCredentialGroupEditorPanel);
            _quickPanelUserLabel.Visible = false;
            _quickPanelUserComboBox.Visible = false;
            _quickPanelCredentialGroupSettingsButton.Visible = false;
            _quickPanelNamespaceLabel.Visible = false;
            _quickPanelNamespaceComboBox.Visible = false;
            _quickPanelCredentialEntryCreateIconButton.Visible = !showCreateGroup;
            _quickPanelCredentialEntryCreateIconButton.Location = new Point(372, 0);
            _quickPanelCredentialEntryCreateIconButton.Text = "+ Create new group";
            _quickPanelCredentialEntryCreateIconButton.BackColor = Color.FromArgb(46, 48, 77);
            _quickPanelCredentialEntryCreateIconButton.ForeColor = Color.FromArgb(120, 205, 0);
            _quickPanelCredentialEntryCreateIconButton.Enabled = !_credentialsLoading && !showCreateGroup;
        }

        private void MoveCredentialGroupActionButton(Control parent)
        {
            if (_quickPanelCredentialEntryCreateIconButton.Parent != parent)
            {
                parent.Controls.Add(_quickPanelCredentialEntryCreateIconButton);
            }
        }

        private void PopulateCredentialAssignedGroupSelector()
        {
            string currentSelection = _quickPanelCredentialAssignedGroupComboBox.SelectedItem is SelectionOption option
                ? option.Value
                : string.Empty;

            _quickPanelCredentialAssignedGroupComboBox.Items.Clear();
            _quickPanelCredentialAssignedGroupComboBox.Items.Add(new SelectionOption("No group", string.Empty));

            foreach (CredentialGroupDto group in _credentialGroups)
            {
                _quickPanelCredentialAssignedGroupComboBox.Items.Add(new SelectionOption(group.GroupName, group.GroupId));
            }

            string preferredSelection = _credentialWorkspaceMode == CredentialWorkspaceMode.CreateEntry
                ? (!string.IsNullOrWhiteSpace(_selectedCredentialGroupId) ? _selectedCredentialGroupId : currentSelection)
                : currentSelection;

            SelectComboBoxValue(_quickPanelCredentialAssignedGroupComboBox, preferredSelection);
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
                CredentialEntryDetail detail = await _credentialCollectionService.ReadEntryDetailAsync(selectedEntry).ConfigureAwait(true);

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
                _quickPanelCredentialStatusLabel.Text = ex.Message;
            }
        }

        private void ApplyCredentialDetailLoadingState()
        {
            _updatingCredentialWorkspaceControls = true;

            try
            {
                _quickPanelCredentialLabelTextBox.Text = string.Empty;
                _quickPanelCredentialDomainTextBox.Text = string.Empty;
                _quickPanelCredentialUsernameTextBox.Text = string.Empty;
                _quickPanelCredentialPasswordTextBox.Text = string.Empty;
                _quickPanelCredentialDescriptionTextBox.Text = string.Empty;
                SetCredentialPasswordVisible(false);

                _quickPanelCredentialLabelTextBox.Enabled = false;
                _quickPanelCredentialAssignedGroupComboBox.Enabled = false;
                _quickPanelCredentialDomainTextBox.Enabled = false;
                _quickPanelCredentialUsernameTextBox.Enabled = false;
                _quickPanelCredentialPasswordTextBox.Enabled = false;
                _quickPanelCredentialDescriptionTextBox.Enabled = false;
                _quickPanelCredentialUsernameCopyButton.Enabled = false;
                _quickPanelCredentialPasswordGenerateButton.Enabled = false;
                _quickPanelCredentialPasswordRevealButton.Enabled = false;
                _quickPanelCredentialPasswordCopyButton.Enabled = false;
                _quickPanelCredentialDescriptionCopyButton.Enabled = false;
                _quickPanelCredentialSaveButton.Enabled = false;
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
                _quickPanelCredentialLabelTextBox.Text = selectedEntry?.Label ?? string.Empty;
                _quickPanelCredentialDomainTextBox.Text = selectedEntry?.Registry.Domain ?? string.Empty;
                _quickPanelCredentialUsernameTextBox.Text = selectedEntry?.Registry.Username ?? string.Empty;
                _quickPanelCredentialPasswordTextBox.Text = selectedEntry?.Registry.Password ?? string.Empty;
                _quickPanelCredentialDescriptionTextBox.Text = selectedEntry?.Registry.Description ?? string.Empty;
                SetCredentialPasswordVisible(false);

                SelectComboBoxValue(_quickPanelCredentialAssignedGroupComboBox, selectedEntry?.GroupId ?? string.Empty);

                bool hasSelection = selectedEntry != null;
                _quickPanelCredentialLabelTextBox.Enabled = hasSelection;
                _quickPanelCredentialAssignedGroupComboBox.Enabled = hasSelection && !_credentialsLoading && !_credentialDetailLoading;
                _quickPanelCredentialDomainTextBox.Enabled = hasSelection;
                _quickPanelCredentialUsernameTextBox.Enabled = hasSelection;
                _quickPanelCredentialPasswordTextBox.Enabled = hasSelection;
                _quickPanelCredentialDescriptionTextBox.Enabled = hasSelection;
                _quickPanelCredentialUsernameCopyButton.Enabled = hasSelection;
                _quickPanelCredentialPasswordGenerateButton.Enabled = hasSelection;
                _quickPanelCredentialPasswordRevealButton.Enabled = hasSelection;
                _quickPanelCredentialPasswordCopyButton.Enabled = hasSelection;
                _quickPanelCredentialDescriptionCopyButton.Enabled = hasSelection;
                _quickPanelCredentialSaveButton.Enabled = hasSelection && !_credentialsLoading && !_credentialDetailLoading;

                if (string.IsNullOrWhiteSpace(_credentialEditorStatusMessage))
                {
                    _quickPanelCredentialStatusLabel.Text = hasSelection
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
            _quickPanelCredentialLabelTextBox.Text = string.Empty;
            _quickPanelCredentialDomainTextBox.Text = string.Empty;
            _quickPanelCredentialUsernameTextBox.Text = string.Empty;
            _quickPanelCredentialPasswordTextBox.Text = string.Empty;
            _quickPanelCredentialDescriptionTextBox.Text = string.Empty;
            SetCredentialPasswordVisible(false);
            SelectComboBoxValue(_quickPanelCredentialAssignedGroupComboBox, _selectedCredentialGroupId);

            _quickPanelCredentialLabelTextBox.Enabled = true;
            _quickPanelCredentialAssignedGroupComboBox.Enabled = !_credentialsLoading;
            _quickPanelCredentialDomainTextBox.Enabled = true;
            _quickPanelCredentialUsernameTextBox.Enabled = true;
            _quickPanelCredentialPasswordTextBox.Enabled = true;
            _quickPanelCredentialDescriptionTextBox.Enabled = true;
            _quickPanelCredentialUsernameCopyButton.Enabled = true;
            _quickPanelCredentialPasswordGenerateButton.Enabled = true;
            _quickPanelCredentialPasswordRevealButton.Enabled = true;
            _quickPanelCredentialPasswordCopyButton.Enabled = true;
            _quickPanelCredentialDescriptionCopyButton.Enabled = true;
            _quickPanelCredentialSaveButton.Enabled = true;

            if (string.IsNullOrWhiteSpace(_credentialEditorStatusMessage))
            {
                _quickPanelCredentialStatusLabel.Text = "Create a new credential for the active server. The current group filter becomes the default group assignment.";
            }
        }

        private void PopulateManagedGroupEditor()
        {
            _quickPanelManagedGroupComboBox.Items.Clear();
            foreach (CredentialGroupDto group in _credentialGroups)
            {
                _quickPanelManagedGroupComboBox.Items.Add(new SelectionOption(group.GroupName, group.GroupId));
            }

            if (string.IsNullOrWhiteSpace(_selectedManagedGroupId))
            {
                _selectedManagedGroupId = _credentialGroups.FirstOrDefault()?.GroupId ?? string.Empty;
            }

            SelectComboBoxValue(_quickPanelManagedGroupComboBox, _selectedManagedGroupId);

            CredentialGroupDto? selectedGroup = _credentialGroups.FirstOrDefault(group =>
                string.Equals(group.GroupId, _selectedManagedGroupId, StringComparison.Ordinal));

            _quickPanelManagedGroupNameTextBox.Text = selectedGroup?.GroupName ?? string.Empty;
            _quickPanelManagedGroupNameTextBox.Enabled = selectedGroup != null && !_credentialsLoading;
            _quickPanelCredentialGroupSaveButton.Enabled = !_credentialsLoading && (_credentialWorkspaceMode == CredentialWorkspaceMode.CreateGroup || selectedGroup != null);
            _quickPanelCredentialGroupDeleteButton.Enabled = !_credentialsLoading && _credentialWorkspaceMode == CredentialWorkspaceMode.ManageGroup && selectedGroup != null;
            _quickPanelCredentialGroupCreateButton.Enabled = !_credentialsLoading;

            if (_credentialWorkspaceMode == CredentialWorkspaceMode.CreateGroup)
            {
                _quickPanelNewGroupNameTextBox.Enabled = !_credentialsLoading;
            }
            else
            {
                _quickPanelNewGroupNameTextBox.Text = string.Empty;
                _quickPanelNewGroupNameTextBox.Enabled = false;
            }

            if (string.IsNullOrWhiteSpace(_groupEditorStatusMessage))
            {
                if (_credentialGroups.Count == 0 && _credentialWorkspaceMode != CredentialWorkspaceMode.CreateGroup) {
                    _quickPanelGroupStatusLabel.Text = "No groups returned for the active server."; 
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
                Domain = _quickPanelCredentialDomainTextBox.Text.Trim(),
                Username = _quickPanelCredentialUsernameTextBox.Text.Trim(),
                Password = _quickPanelCredentialPasswordTextBox.Text,
                Description = _quickPanelCredentialDescriptionTextBox.Text.Trim()
            };
        }

        private string GetSelectedAssignedCredentialGroupId()
        {
            return _quickPanelCredentialAssignedGroupComboBox.SelectedItem is SelectionOption option
                ? option.Value
                : string.Empty;
        }

        private void ClearCredentialEditorStatus()
        {
            _credentialEditorStatusMessage = string.Empty;
            _quickPanelCredentialStatusLabel.Text = string.Empty;
        }

        private void SetCredentialPasswordVisible(bool visible)
        {
            _quickPanelCredentialPasswordTextBox.UseSystemPasswordChar = !visible;
        }

        private void SetCredentialEditorStatus(string message)
        {
            _credentialEditorStatusMessage = message;
            _quickPanelCredentialStatusLabel.Text = message;
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
            _quickPanelGroupStatusLabel.Text = string.Empty;
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

        private void contextMenuStrip1_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isDeviceRegistered)
            {
                _showExpandedTrayMenu = true;
            }

            _userSelectionMenuController.RefreshMenu();
            _namespaceSelectionMenuController.RefreshMenu();
            RefreshQuickPanelSelections();
            UpdateAuthenticationMenuItems();
            ConfigureTrayMenuPresentation();
        }

        private void contextMenuStrip1_Closed(object? sender, ToolStripDropDownClosedEventArgs e)
        {
            if (_reopenExpandedTrayMenuRequested)
            {
                _reopenExpandedTrayMenuRequested = false;
                BeginInvoke(new Action(ShowExpandedTrayMenu));
                return;
            }

            if (_explicitQuickPanelCloseRequested)
            {
                _explicitQuickPanelCloseRequested = false;
                _showExpandedTrayMenu = false;
            }
        }

        private void contextMenuStrip1_Closing(object? sender, ToolStripDropDownClosingEventArgs e)
        {
            if (_reopenExpandedTrayMenuRequested || _explicitQuickPanelCloseRequested)
            {
                return;
            }

            if (_showExpandedTrayMenu && _isDeviceRegistered)
            {
                e.Cancel = true;
            }
        }

        private void ConfigureTrayMenuPresentation()
        {
            bool expanded = _showExpandedTrayMenu && _isDeviceRegistered;

            contextMenuStrip1.AutoSize = !expanded;
            contextMenuStrip1.BackColor = expanded
                ? Color.FromArgb(36, 38, 63)
                : Color.FromArgb(19, 33, 50);
            contextMenuStrip1.Padding = expanded ? Padding.Empty : new Padding(10, 10, 10, 8);
            contextMenuStrip1.MinimumSize = expanded ? Size.Empty : new Size(240, 0);
            contextMenuStrip1.Size = expanded
                ? new Size(_quickPanelContainer.Width + contextMenuStrip1.Padding.Horizontal, _quickPanelContainer.Height + contextMenuStrip1.Padding.Vertical)
                : new Size(240, 0);

            exitToolStripMenuItem.Visible = !expanded;

            if (_quickPanelHost != null)
            {
                _quickPanelHost.Visible = expanded;
                _quickPanelHost.Size = _quickPanelContainer.Size;
            }

            contextMenuStrip1.PerformLayout();
        }

        private void newUserToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            ShowBaseUrlPrompt();
        }

        private void openToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (_isDeviceRegistered)
            {
                _showExpandedTrayMenu = true;

                if (contextMenuStrip1.Visible)
                {
                    _reopenExpandedTrayMenuRequested = true;
                    contextMenuStrip1.Close();
                    return;
                }

                ShowExpandedTrayMenu();
                return;
            }

            ShowBaseUrlPrompt(showAsTrayPrompt: true);
        }

        private async void quickPanelRefreshButton_Click(object? sender, EventArgs e)
        {
            await RefreshQuickPanelFromBackendAsync().ConfigureAwait(true);
        }

        private void quickPanelHideButton_Click(object? sender, EventArgs e)
        {
            CloseQuickPanel();
        }

        private async void quickPanelPrimaryActionButton_Click(object? sender, EventArgs e)
        {
            if (_authenticationInProgress)
            {
                return;
            }

            if (_authenticationService.IsAuthorized)
            {
                logoutToolStripMenuItem_Click(sender!, EventArgs.Empty);
                return;
            }

            loginToolStripMenuItem_Click(sender, EventArgs.Empty);
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
                    loginToolStripMenuItem_Click(sender, EventArgs.Empty);
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
                if (_isDeviceRegistered)
                {
                    ShowExpandedTrayMenu();
                }
                else
                {
                    ShowBaseUrlPrompt(showAsTrayPrompt: true);
                }

                return;
            }
        }

        private void notifyIcon1_DoubleClick(object? sender, EventArgs e)
        {
            if (_isDeviceRegistered)
            {
                ShowExpandedTrayMenu();
                return;
            }

            ShowBaseUrlPrompt(showAsTrayPrompt: true);
        }

        private void quickPanelUserComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_updatingQuickPanelSelections || _quickPanelUserComboBox.SelectedItem is not SelectionOption option)
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
            _namespaceSelectionMenuController.RefreshMenu();
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
            if (_updatingQuickPanelSelections || _quickPanelNamespaceComboBox.SelectedItem is not SelectionOption option)
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

        private void HandleTopMenuNamespaceSelectionChanged()
        {
            _credentialCollectionCacheKey = string.Empty;
            _credentialCollectionLoaded = false;
            ResetDevicesState();
            RefreshQuickPanelSelections();
            UpdateQuickPanelState();

            if (_trayMenuController?.ActiveSection == TrayMenuSection.Credentials &&
                _authenticationService.IsAuthorizedForActiveNamespace)
            {
                _ = EnsureCredentialCollectionLoadedAsync(forceRefresh: true);
                return;
            }

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

            _quickPanelSettingsUserActionComboBox.Items.Clear();
            _quickPanelSettingsUserActionComboBox.Items.Add(new SelectionOption("Select profile action", string.Empty));
            if (canCreateProfile)
            {
                _quickPanelSettingsUserActionComboBox.Items.Add(new SelectionOption("Create new profile", "create"));
            }

            if (canDeleteProfile)
            {
                _quickPanelSettingsUserActionComboBox.Items.Add(new SelectionOption("Delete profile", "delete"));
            }
            _quickPanelSettingsUserActionComboBox.SetSelectedIndexSilently(0);
        }

        private void quickPanelSettingsUserActionComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_quickPanelSettingsUserActionComboBox.SelectedItem is not SelectionOption option ||
                _trayMenuController?.ActiveSection != TrayMenuSection.SettingsUsers)
            {
                return;
            }

            _quickPanelSettingsUserActionComboBox.SetSelectedIndexSilently(0);

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

            try
            {
                _authenticationInProgress = true;
                UpdateAuthenticationMenuItems();

                string activeDesktopDeviceId = AppEnvironment.GetActiveNamespaceDesktopDeviceId();
                await _accountDeviceService.DeleteDeviceAsync(new AccountDeviceDto
                {
                    DesktopDeviceId = activeDesktopDeviceId
                }).ConfigureAwait(true);

                var configStore = new RegistrationConfigStore();
                if (!configStore.RemoveRegistration(activeUserId))
                {
                    throw new InvalidOperationException("The active user profile could not be removed.");
                }

                _authenticationService.RevokeAuthorization();
                _showInlineUserDeletion = false;

                bool hasRemainingProfiles = configStore.TryReadRegistrations(out List<DesktopRegistration>? registrations) &&
                    registrations is { Count: > 0 };

                if (!hasRemainingProfiles)
                {
                    AppEnvironment.ClearActiveSelection();
                    _isDeviceRegistered = false;
                    _extensionSessionStateStore.MarkInvalid("user_profile_deleted");
                    CloseQuickPanel();
                }
                else
                {
                    DesktopRegistration nextRegistration = registrations![0];
                    AppEnvironment.SetActiveUser(nextRegistration.UserKey);
                    _isDeviceRegistered = true;
                    _extensionSessionStateStore.MarkInvalid("authorization_revoked");
                    _trayMenuController?.SetActiveSection(TrayMenuSection.Login, triggerStateChanged: true);
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Delete user profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _authenticationInProgress = false;
                _userSelectionMenuController.RefreshMenu();
                _namespaceSelectionMenuController.RefreshMenu();
                RefreshQuickPanelSelections();
                UpdateQuickPanelState();
                UpdateAuthenticationMenuItems();
            }
        }

        private void PopulateSettingsServerActions()
        {
            bool canManageServers = _registeredDeviceService.TryReadRegistration(out DesktopRegistration? registration) &&
                registration is { Namespaces.Count: > 0 };

            int expectedItems = canManageServers ? 4 : 3;
            if (_quickPanelSettingsServerActionComboBox.Items.Count == expectedItems)
            {
                return;
            }

            _quickPanelSettingsServerActionComboBox.Items.Clear();
            _quickPanelSettingsServerActionComboBox.Items.Add(new SelectionOption("Select server action", string.Empty));
            _quickPanelSettingsServerActionComboBox.Items.Add(new SelectionOption("Add a server", "register"));
            _quickPanelSettingsServerActionComboBox.Items.Add(new SelectionOption("Sync existing server", "sync"));

            if (canManageServers)
            {
                _quickPanelSettingsServerActionComboBox.Items.Add(new SelectionOption("Manage subscription...", "manage"));
            }

            _quickPanelSettingsServerActionComboBox.SetSelectedIndexSilently(0);
        }

        private void quickPanelSettingsServerActionComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_quickPanelSettingsServerActionComboBox.SelectedItem is not SelectionOption option ||
                _trayMenuController?.ActiveSection != TrayMenuSection.SettingsServers)
            {
                return;
            }

            _quickPanelSettingsServerActionComboBox.SetSelectedIndexSilently(0);

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
            _quickPanelCredentialPasswordTextBox.Text = GenerateCredentialPassword();
            SetCredentialPasswordVisible(false);
            SetCredentialEditorStatus("Password generated.");
        }

        private void quickPanelCredentialPasswordRevealButton_Click(object? sender, EventArgs e)
        {
            SetCredentialPasswordVisible(_quickPanelCredentialPasswordTextBox.UseSystemPasswordChar);
        }

        private void quickPanelCredentialPasswordCopyButton_Click(object? sender, EventArgs e)
        {
            CopyCredentialValueToClipboard(_quickPanelCredentialPasswordTextBox.Text, "Password");
        }

        private void quickPanelCredentialUsernameCopyButton_Click(object? sender, EventArgs e)
        {
            CopyCredentialValueToClipboard(_quickPanelCredentialUsernameTextBox.Text, "Username");
        }

        private void quickPanelCredentialDescriptionCopyButton_Click(object? sender, EventArgs e)
        {
            CopyCredentialValueToClipboard(_quickPanelCredentialDescriptionTextBox.Text, "Description");
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

            string label = _quickPanelCredentialLabelTextBox.Text.Trim();
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

            string editedGroupName = _quickPanelManagedGroupNameTextBox.Text.Trim();

            try
            {
                _credentialsLoading = true;
                ClearGroupEditorStatus();
                RenderCredentialWorkspace();

                if (_credentialWorkspaceMode == CredentialWorkspaceMode.CreateGroup)
                {
                    string newGroupName = _quickPanelNewGroupNameTextBox.Text.Trim();
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
                _quickPanelNewGroupNameTextBox.Text = string.Empty;
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
            if (_updatingCredentialWorkspaceControls || _quickPanelManagedGroupComboBox.SelectedItem is not SelectionOption option)
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
