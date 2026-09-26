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
        private readonly Label _quickPanelEyebrowLabel = new();
        private readonly Label _quickPanelTitleLabel = new();
        private readonly Label _quickPanelStatusLabel = new();
        private readonly Label _quickPanelSummaryLabel = new();
        private readonly FlowLayoutPanel _quickPanelServerStatusPanel = new();
        private readonly Label _quickPanelMenuSectionLabel = new();
        private readonly FlowLayoutPanel _quickPanelNavigationPanel = new();
        private readonly Button _quickPanelLoginNavButton = new();
        private readonly FlowLayoutPanel _quickPanelAccountsPanel = new();
        private readonly Button _quickPanelCredentialsNavButton = new();
        private readonly Button _quickPanelSettingsNavButton = new();
        private readonly ContextMenuStrip _settingsHoverMenu = new();
        private readonly ToolStripMenuItem _settingsUsersMenuItem = new("Users");
        private readonly ToolStripMenuItem _settingsServersMenuItem = new("Servers");
        private readonly ToolStripMenuItem _settingsDevicesMenuItem = new("Devices");
        private readonly ToolStripMenuItem _settingsAccountMenuItem = new("Account");
        private readonly System.Windows.Forms.Timer _settingsHoverCloseTimer = new() { Interval = 150 };
        private readonly Label _quickPanelActionSectionLabel = new();
        private readonly Label _quickPanelDetailEyebrowLabel = new();
        private readonly Label _quickPanelDetailTitleLabel = new();
        private readonly Label _quickPanelDetailDescriptionLabel = new();
        private readonly Panel _quickPanelMoveHandle = new();
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
        private readonly Button _quickPanelRegisterUserInlineButton = new();
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
        private readonly Button _quickPanelCredentialSaveButton = new();
        private readonly Button _quickPanelCredentialGroupSaveButton = new();
        private readonly Button _quickPanelCredentialGroupCreateButton = new();
        private readonly Label _quickPanelCredentialStatusLabel = new();
        private readonly Label _quickPanelGroupStatusLabel = new();
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
        private bool _inlineUserRegistrationSubmitting;
        private bool _inlineUserRegistrationAwaitingCompletion;
        private bool _updatingCredentialWorkspaceControls;
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

        private enum InlineServerRegistrationMode
        {
            NamespaceSync,
            NewServer
        }

        private readonly record struct TrayServerStatus(string Name, bool IsConnected, bool IsActive);

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
            ApplyRegistrationLayout();
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
            _quickPanelContainer.Size = new Size(820, 660);
            _quickPanelContainer.MouseDown += quickPanelContainer_MouseDown;
            _quickPanelContainer.MouseMove += quickPanelContainer_MouseMove;
            _quickPanelContainer.MouseUp += quickPanelContainer_MouseUp;

            _quickPanelMenuPanel.BackColor = Color.FromArgb(36, 38, 63);
            _quickPanelMenuPanel.Location = new Point(0, 0);
            _quickPanelMenuPanel.Size = new Size(176, 660);

            _quickPanelDividerPanel.BackColor = Color.FromArgb(59, 61, 88);
            _quickPanelDividerPanel.Location = new Point(176, 0);
            _quickPanelDividerPanel.Size = new Size(1, 660);

            _quickPanelDetailPanel.BackColor = Color.FromArgb(31, 33, 57);
            _quickPanelDetailPanel.Location = new Point(177, 0);
            _quickPanelDetailPanel.Size = new Size(643, 660);

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

            ConfigureQuickPanelNavigationButton(_quickPanelLoginNavButton, "Login");
            ConfigureQuickPanelNavigationButton(_quickPanelCredentialsNavButton, "Credentials");
            ConfigureQuickPanelNavigationButton(_quickPanelSettingsNavButton, "Settings");
            InitializeSettingsHoverMenu();

            _quickPanelNavigationPanel.Controls.Add(_quickPanelLoginNavButton);
            _quickPanelNavigationPanel.Controls.Add(_quickPanelCredentialsNavButton);
            _quickPanelNavigationPanel.Controls.Add(_quickPanelSettingsNavButton);

            _trayMenuController = new TrayMenuController(UpdateQuickPanelNavigationButtonState, HandleTrayMenuSectionChanged);
            _trayMenuController.Register(TrayMenuSection.Login, _quickPanelLoginNavButton);
            _trayMenuController.Register(TrayMenuSection.Credentials, _quickPanelCredentialsNavButton);

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
            ConfigureQuickPanelHeaderIconButton(_quickPanelHideButton, new Point(514, 16), "Minimize", DrawQuickPanelMinimizeIcon);
            _quickPanelHideButton.Click += quickPanelHideButton_Click;

            ConfigureQuickPanelButton(_quickPanelExitButton, Color.FromArgb(45, 51, 60), Color.White);
            ConfigureQuickPanelHeaderIconButton(_quickPanelExitButton, new Point(548, 16), "Exit", DrawQuickPanelExitIcon);
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
            _quickPanelDetailTitleLabel.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
            _quickPanelDetailTitleLabel.ForeColor = Color.White;
            _quickPanelDetailTitleLabel.Location = new Point(32, 52);
            _quickPanelDetailTitleLabel.Text = "Tray overview";
            ConfigureQuickPanelDragHandle(_quickPanelDetailTitleLabel);

            _quickPanelDetailDescriptionLabel.AutoSize = false;
            _quickPanelDetailDescriptionLabel.Font = new Font("Segoe UI", 10F);
            _quickPanelDetailDescriptionLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelDetailDescriptionLabel.Location = new Point(36, 98);
            _quickPanelDetailDescriptionLabel.Size = new Size(574, 44);

            _quickPanelMoveHandle.BackColor = Color.FromArgb(46, 48, 77);
            _quickPanelMoveHandle.Location = new Point(582, 16);
            _quickPanelMoveHandle.Size = new Size(28, 28);
            _quickPanelMoveHandle.Cursor = Cursors.SizeAll;
            _quickPanelMoveHandle.Paint += quickPanelMoveHandle_Paint;
            _quickPanelMoveHandle.MouseEnter += (_, _) =>
            {
                _quickPanelMoveHandle.BackColor = Color.FromArgb(120, 205, 0);
                _quickPanelMoveHandle.Invalidate();
            };
            _quickPanelMoveHandle.MouseLeave += (_, _) =>
            {
                _quickPanelMoveHandle.BackColor = Color.FromArgb(46, 48, 77);
                _quickPanelMoveHandle.Invalidate();
            };
            ConfigureQuickPanelDragHandle(_quickPanelMoveHandle);

            _quickPanelSelectionCard.BackColor = Color.FromArgb(41, 43, 71);
            _quickPanelSelectionCard.BorderStyle = BorderStyle.None;
            _quickPanelSelectionCard.Location = new Point(32, 152);
            _quickPanelSelectionCard.Size = new Size(578, 126);

            _quickPanelUserLabel.AutoSize = true;
            _quickPanelUserLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelUserLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelUserLabel.Location = new Point(16, 14);
            _quickPanelUserLabel.Text = "Active user";

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

            _quickPanelCredentialGroupSettingsButton.Size = new Size(32, 25);
            _quickPanelCredentialGroupSettingsButton.Location = new Point(266, 36);
            _quickPanelCredentialGroupSettingsButton.FlatStyle = FlatStyle.Flat;
            _quickPanelCredentialGroupSettingsButton.FlatAppearance.BorderSize = 0;
            _quickPanelCredentialGroupSettingsButton.BackColor = Color.FromArgb(46, 48, 77);
            _quickPanelCredentialGroupSettingsButton.ForeColor = Color.White;
            _quickPanelCredentialGroupSettingsButton.Font = new Font("Segoe UI Symbol", 10F, FontStyle.Bold);
            _quickPanelCredentialGroupSettingsButton.Text = "⚙";
            _quickPanelCredentialGroupSettingsButton.UseVisualStyleBackColor = false;
            _quickPanelCredentialGroupSettingsButton.Visible = false;
            _quickPanelCredentialGroupSettingsButton.Click += quickPanelCredentialManageGroupsButton_Click;

            _quickPanelCredentialEntryCreateIconButton.Size = new Size(32, 25);
            _quickPanelCredentialEntryCreateIconButton.Location = new Point(528, 36);
            _quickPanelCredentialEntryCreateIconButton.FlatStyle = FlatStyle.Flat;
            _quickPanelCredentialEntryCreateIconButton.FlatAppearance.BorderSize = 0;
            _quickPanelCredentialEntryCreateIconButton.BackColor = Color.FromArgb(46, 48, 77);
            _quickPanelCredentialEntryCreateIconButton.ForeColor = Color.White;
            _quickPanelCredentialEntryCreateIconButton.Font = new Font("Segoe UI Symbol", 10F, FontStyle.Bold);
            _quickPanelCredentialEntryCreateIconButton.Text = "+";
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
            _quickPanelSelectionCard.Controls.Add(_quickPanelRegisterUserInlineButton);
            _quickPanelSelectionCard.Controls.Add(_quickPanelRegisterNewServerInlineButton);
            _quickPanelSelectionCard.Controls.Add(_quickPanelServerRegistrationInlineButton);
            _quickPanelSelectionCard.Controls.Add(_quickPanelServerStatusValueLabel);
            _quickPanelSelectionCard.Controls.Add(_quickPanelServerLoginInlineButton);

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

            ConfigureQuickPanelInlineActionButton(_quickPanelRegisterUserInlineButton, "+ Register user", new Point(420, 12), quickPanelRegisterUserInlineButton_Click);
            _quickPanelRegisterUserInlineButton.Size = new Size(140, 28);
            _quickPanelRegisterUserInlineButton.ForeColor = Color.FromArgb(120, 205, 0);

            ConfigureQuickPanelInlineActionButton(_quickPanelRegisterNewServerInlineButton, "+ Register new server", new Point(16, 14), quickPanelRegisterNewServerInlineButton_Click);
            _quickPanelRegisterNewServerInlineButton.Size = new Size(156, 28);
            _quickPanelRegisterNewServerInlineButton.ForeColor = Color.FromArgb(120, 205, 0);

            ConfigureQuickPanelInlineActionButton(_quickPanelServerRegistrationInlineButton, "+ Sync already registered", new Point(180, 14), quickPanelServerRegistrationInlineButton_Click);
            _quickPanelServerRegistrationInlineButton.Size = new Size(170, 28);
            _quickPanelServerRegistrationInlineButton.ForeColor = Color.FromArgb(120, 205, 0);

            _quickPanelServerStatusValueLabel.AutoSize = false;
            _quickPanelServerStatusValueLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _quickPanelServerStatusValueLabel.Location = new Point(214, 16);
            _quickPanelServerStatusValueLabel.Size = new Size(108, 24);
            _quickPanelServerStatusValueLabel.TextAlign = ContentAlignment.MiddleCenter;
            _quickPanelServerStatusValueLabel.Visible = false;

            ConfigureQuickPanelInlineActionButton(_quickPanelServerLoginInlineButton, "Login", new Point(438, 14), quickPanelPrimaryActionButton_Click);
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
            _quickPanelDetailPanel.Controls.Add(_quickPanelMoveHandle);
            _quickPanelDetailPanel.Controls.Add(_quickPanelSelectionCard);
            _quickPanelDetailPanel.Controls.Add(_quickPanelMetricsCard);
            _quickPanelDetailPanel.Controls.Add(_quickPanelInsightCard);
            _quickPanelDetailPanel.Controls.Add(_quickPanelCredentialWorkspacePanel);
            InitializeInlineUserRegistrationPanel();
            _quickPanelDetailPanel.Controls.Add(_quickPanelUserRegistrationPanel);

            _quickPanelAccountsPanel.FlowDirection = FlowDirection.TopDown;
            _quickPanelAccountsPanel.WrapContents = false;
            _quickPanelAccountsPanel.AutoScroll = true;
            _quickPanelAccountsPanel.Location = new Point(32, 152);
            _quickPanelAccountsPanel.Size = new Size(578, 390);
            _quickPanelAccountsPanel.Visible = false;
            _quickPanelDetailPanel.Controls.Add(_quickPanelAccountsPanel);

            _quickPanelContainer.Controls.Add(_quickPanelMenuPanel);
            _quickPanelContainer.Controls.Add(_quickPanelDividerPanel);
            _quickPanelContainer.Controls.Add(_quickPanelDetailPanel);

            ResumeLayout(performLayout: false);
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
            Color defaultBackColor = button.BackColor;
            Color defaultForeColor = button.ForeColor;
            button.AccessibleName = toolTipText;
            button.Location = location;
            button.Size = new Size(28, 28);
            button.Margin = Padding.Empty;
            button.Padding = Padding.Empty;
            button.Text = string.Empty;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Paint += paintHandler;
            button.MouseEnter += (_, _) =>
            {
                button.BackColor = Color.FromArgb(120, 205, 0);
                button.ForeColor = Color.FromArgb(24, 30, 24);
                button.FlatAppearance.BorderColor = button.BackColor;
            };
            button.MouseLeave += (_, _) =>
            {
                button.BackColor = defaultBackColor;
                button.ForeColor = defaultForeColor;
                button.FlatAppearance.BorderColor = defaultBackColor;
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

        private void InitializeSettingsHoverMenu()
        {
            _settingsHoverMenu.ShowImageMargin = false;
            _settingsHoverMenu.BackColor = Color.FromArgb(36, 38, 63);
            _settingsHoverMenu.ForeColor = Color.White;
            _settingsHoverMenu.RenderMode = ToolStripRenderMode.Professional;
            _settingsHoverMenu.Renderer = new ToolStripProfessionalRenderer(new TrayMenuColorTable());

            ConfigureSettingsHoverMenuItem(_settingsUsersMenuItem);
            ConfigureSettingsHoverMenuItem(_settingsServersMenuItem);
            ConfigureSettingsHoverMenuItem(_settingsDevicesMenuItem);
            ConfigureSettingsHoverMenuItem(_settingsAccountMenuItem);

            _settingsUsersMenuItem.Click += settingsUsersMenuItem_Click;
            _settingsServersMenuItem.Click += settingsServersMenuItem_Click;
            _settingsAccountMenuItem.Click += settingsAccountMenuItem_Click;

            _settingsHoverMenu.Items.AddRange(new ToolStripItem[]
            {
                _settingsUsersMenuItem,
                _settingsServersMenuItem,
                _settingsDevicesMenuItem,
                _settingsAccountMenuItem
            });

            _quickPanelSettingsNavButton.MouseEnter += settingsNavButton_MouseEnter;
            _quickPanelSettingsNavButton.MouseLeave += settingsNavButton_MouseLeave;
            _settingsHoverMenu.MouseEnter += settingsHoverMenu_MouseEnter;
            _settingsHoverMenu.MouseLeave += settingsHoverMenu_MouseLeave;
            _settingsHoverCloseTimer.Tick += settingsHoverCloseTimer_Tick;
        }

        private static void ConfigureSettingsHoverMenuItem(ToolStripMenuItem menuItem)
        {
            menuItem.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            menuItem.Padding = new Padding(8, 6, 8, 6);
            menuItem.ForeColor = Color.White;
        }

        private void settingsUsersMenuItem_Click(object? sender, EventArgs e)
        {
            _settingsHoverMenu.Close();
            _trayMenuController?.SetActiveSection(TrayMenuSection.SettingsUsers, triggerStateChanged: true);
        }

        private void settingsAccountMenuItem_Click(object? sender, EventArgs e)
        {
            _settingsHoverMenu.Close();
            _trayMenuController?.SetActiveSection(TrayMenuSection.Accounts, triggerStateChanged: true);
        }

        private void settingsServersMenuItem_Click(object? sender, EventArgs e)
        {
            _settingsHoverMenu.Close();
            _trayMenuController?.SetActiveSection(TrayMenuSection.SettingsServers, triggerStateChanged: true);
        }

        private void settingsNavButton_MouseEnter(object? sender, EventArgs e)
        {
            _settingsHoverCloseTimer.Stop();

            if (!_showExpandedTrayMenu || _settingsHoverMenu.Visible)
            {
                return;
            }

            Point location = _quickPanelSettingsNavButton.PointToScreen(new Point(_quickPanelSettingsNavButton.Width + 4, 0));
            _settingsHoverMenu.Show(location);
        }

        private void settingsNavButton_MouseLeave(object? sender, EventArgs e)
        {
            StartSettingsHoverCloseTimer();
        }

        private void settingsHoverMenu_MouseEnter(object? sender, EventArgs e)
        {
            _settingsHoverCloseTimer.Stop();
        }

        private void settingsHoverMenu_MouseLeave(object? sender, EventArgs e)
        {
            StartSettingsHoverCloseTimer();
        }

        private void StartSettingsHoverCloseTimer()
        {
            if (_settingsHoverMenu.Visible)
            {
                _settingsHoverCloseTimer.Start();
            }
        }

        private void settingsHoverCloseTimer_Tick(object? sender, EventArgs e)
        {
            if (IsPointerOver(_quickPanelSettingsNavButton) || _settingsHoverMenu.Bounds.Contains(Cursor.Position))
            {
                return;
            }

            _settingsHoverCloseTimer.Stop();
            _settingsHoverMenu.Close();
        }

        private static bool IsPointerOver(Control control)
        {
            return control.RectangleToScreen(control.ClientRectangle).Contains(Cursor.Position);
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

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialLabelEditorLabel, "Label", new Point(0, 0));
            ConfigureCredentialWorkspaceTextBox(_quickPanelCredentialLabelTextBox, new Point(0, 20), new Size(257, 25), multiline: false);
            _quickPanelCredentialLabelTextBox.TextChanged += credentialEditorInput_TextChanged;

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialAssignedGroupLabel, "Assigned group", new Point(272, 0));
            ConfigureCredentialWorkspaceComboBox(_quickPanelCredentialAssignedGroupComboBox, new Point(272, 20), new Size(275, 25));
            _quickPanelCredentialAssignedGroupComboBox.SelectedIndexChanged += credentialEditorInput_Changed;

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialDomainLabel, "Domain", new Point(0, 58));
            ConfigureCredentialWorkspaceTextBox(_quickPanelCredentialDomainTextBox, new Point(0, 78), new Size(166, 25), multiline: false);
            _quickPanelCredentialDomainTextBox.TextChanged += credentialEditorInput_TextChanged;

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialUsernameLabel, "Username", new Point(178, 58));
            ConfigureCredentialWorkspaceTextBox(_quickPanelCredentialUsernameTextBox, new Point(178, 78), new Size(166, 25), multiline: false);
            _quickPanelCredentialUsernameTextBox.TextChanged += credentialEditorInput_TextChanged;
            ConfigureCredentialWorkspaceIconButton(_quickPanelCredentialUsernameCopyButton, "⧉", new Point(250, 55), quickPanelCredentialUsernameCopyButton_Click);

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialPasswordLabel, "Password", new Point(356, 58));
            ConfigureCredentialWorkspaceTextBox(_quickPanelCredentialPasswordTextBox, new Point(356, 78), new Size(191, 25), multiline: false);
            _quickPanelCredentialPasswordTextBox.UseSystemPasswordChar = true;
            _quickPanelCredentialPasswordTextBox.TextChanged += credentialEditorInput_TextChanged;
            ConfigureCredentialWorkspaceIconButton(_quickPanelCredentialPasswordGenerateButton, "↻", new Point(425, 55), quickPanelCredentialPasswordGenerateButton_Click);
            ConfigureCredentialWorkspaceIconButton(_quickPanelCredentialPasswordRevealButton, "👁", new Point(501, 55), quickPanelCredentialPasswordRevealButton_Click);
            ConfigureCredentialWorkspaceIconButton(_quickPanelCredentialPasswordCopyButton, "⧉", new Point(525, 55), quickPanelCredentialPasswordCopyButton_Click);

            ConfigureCredentialWorkspaceLabel(_quickPanelCredentialDescriptionLabel, "Description", new Point(0, 120));
            ConfigureCredentialWorkspaceTextBox(_quickPanelCredentialDescriptionTextBox, new Point(0, 140), new Size(547, 135), multiline: true);
            _quickPanelCredentialDescriptionTextBox.TextChanged += credentialEditorInput_TextChanged;
            ConfigureCredentialWorkspaceIconButton(_quickPanelCredentialDescriptionCopyButton, "⧉", new Point(88, 117), quickPanelCredentialDescriptionCopyButton_Click);

            ConfigureCredentialWorkspaceButton(_quickPanelCredentialSaveButton, "Save changes", new Point(0, 291), quickPanelCredentialSaveButton_Click);
            _quickPanelCredentialSaveButton.BackColor = Color.FromArgb(120, 205, 0);
            _quickPanelCredentialSaveButton.ForeColor = Color.Black;
            _quickPanelCredentialSaveButton.FlatAppearance.BorderSize = 1;
            _quickPanelCredentialSaveButton.FlatAppearance.BorderColor = Color.FromArgb(120, 205, 0);

            _quickPanelCredentialStatusLabel.AutoSize = false;
            _quickPanelCredentialStatusLabel.Font = new Font("Segoe UI", 8.75F);
            _quickPanelCredentialStatusLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelCredentialStatusLabel.Location = new Point(0, 331);
            _quickPanelCredentialStatusLabel.Size = new Size(547, 32);

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
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialSaveButton);
            _quickPanelCredentialEntryEditorPanel.Controls.Add(_quickPanelCredentialStatusLabel);

            _quickPanelCredentialGroupEditorPanel.Location = new Point(16, 12);
            _quickPanelCredentialGroupEditorPanel.Size = new Size(530, 220);
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

            ConfigureCredentialWorkspaceButton(_quickPanelCredentialGroupSaveButton, "Save group", new Point(0, 122), quickPanelCredentialGroupSaveButton_Click);
            ConfigureCredentialWorkspaceButton(_quickPanelCredentialGroupCreateButton, "Create group", new Point(128, 122), quickPanelCredentialGroupCreateButton_Click);

            _quickPanelGroupStatusLabel.AutoSize = false;
            _quickPanelGroupStatusLabel.Font = new Font("Segoe UI", 8.75F);
            _quickPanelGroupStatusLabel.ForeColor = Color.FromArgb(171, 174, 211);
            _quickPanelGroupStatusLabel.Location = new Point(0, 162);
            _quickPanelGroupStatusLabel.Size = new Size(522, 40);

            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelManagedGroupLabel);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelManagedGroupComboBox);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelManagedGroupNameLabel);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelManagedGroupNameTextBox);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelNewGroupNameLabel);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelNewGroupNameTextBox);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelCredentialGroupSaveButton);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelCredentialGroupCreateButton);
            _quickPanelCredentialGroupEditorPanel.Controls.Add(_quickPanelGroupStatusLabel);

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
            ConfigureCredentialWorkspaceLabel(_quickPanelUserRegistrationNameLabel, "User name", new Point(16, 82));
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
            _quickPanelContainer.Size = new Size(820, 660);

            if (_quickPanelHost != null)
            {
                _quickPanelHost.Size = _quickPanelContainer.Size;
            }
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
                _ = EnsureCredentialCollectionLoadedAsync();
            }
        }

        private void RefreshQuickPanelSelections()
        {
            if (!_isDeviceRegistered)
            {
                return;
            }

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
            RefreshQuickPanelServerStatusList(state.Registration);
            LayoutQuickPanelMenuSections();

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
            bool usersSectionEnabled = !_authenticationService.IsAuthorized;
            bool credentialsSectionEnabled = _authenticationService.IsAuthorizedForActiveNamespace;

            if (!usersSectionEnabled && _trayMenuController?.ActiveSection == TrayMenuSection.Users)
            {
                _trayMenuController.SetActiveSection(TrayMenuSection.Login);
            }

            if (!credentialsSectionEnabled && _trayMenuController?.ActiveSection == TrayMenuSection.Credentials)
            {
                _trayMenuController.SetActiveSection(TrayMenuSection.Servers);
            }

            _quickPanelCredentialsNavButton.Enabled = credentialsSectionEnabled;
            _quickPanelCredentialsNavButton.Cursor = credentialsSectionEnabled ? Cursors.Hand : Cursors.Default;

            if (_trayMenuController == null)
            {
                return;
            }

            UpdateQuickPanelNavigationButtonState(_quickPanelLoginNavButton, _trayMenuController.ActiveSection == TrayMenuSection.Login);
            bool settingsSectionActive = _trayMenuController.ActiveSection is
                TrayMenuSection.SettingsUsers or
                TrayMenuSection.SettingsServers or
                TrayMenuSection.Accounts ||
                _trayMenuController.ActiveSection == TrayMenuSection.Servers && _showInlineServerRegistration;
            UpdateQuickPanelNavigationButtonState(_quickPanelSettingsNavButton, settingsSectionActive);
            UpdateQuickPanelNavigationButtonState(_quickPanelCredentialsNavButton, _trayMenuController.ActiveSection == TrayMenuSection.Credentials);
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
                return new[] { new TrayServerStatus("No servers", false, false) };
            }

            string? activeNamespaceId = AppEnvironment.GetActiveNamespaceId();

            return registration.Namespaces
                .Select(namespaceItem => new TrayServerStatus(
                    string.IsNullOrWhiteSpace(namespaceItem.Namespace) ? namespaceItem.Url : namespaceItem.Namespace,
                    _authorizationSessionStore.HasActiveSessionForNamespace(registration.UserKey, namespaceItem.NamespaceId),
                    string.Equals(namespaceItem.NamespaceId, activeNamespaceId, StringComparison.Ordinal)))
                .ToArray();
        }

        private static Control CreateQuickPanelServerStatusRow(string serverName, bool isConnected, bool isActive)
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
            return rowPanel;
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
            string activeUserLabel = registration?.DisplayUserName ?? "No active user";
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
                RegisteredServers = registration?.Namespaces.Select(item => item.Namespace).ToArray() ?? Array.Empty<string>(),
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
            _quickPanelUserLabel.Text = credentialsLayout ? "Group" : "Active user";
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
        }

        private void UpdateAccountsPresentation()
        {
            bool accountsActive = _trayMenuController?.ActiveSection == TrayMenuSection.Accounts;
            _quickPanelAccountsPanel.Visible = accountsActive;

            if (!accountsActive)
            {
                return;
            }

            _quickPanelSelectionCard.Visible = false;
            _quickPanelMetricsCard.Visible = false;
            _quickPanelInsightCard.Visible = false;
            _quickPanelAccountsPanel.Controls.Clear();

            if (!_registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) || registrations == null)
            {
                return;
            }

            foreach (DesktopRegistration registration in registrations)
            {
                foreach (NamespaceItemDto namespaceItem in registration.Namespaces)
                {
                    _quickPanelAccountsPanel.Controls.Add(CreateAccountRow(registration.UserKey, namespaceItem));
                }
            }
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
                "Deleting this account deregisters this device and permanently removes all credentials saved on this server. This cannot be undone.",
                "Delete account?",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmation != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _accountLifecycleService.DeleteAccountAsync(userId, namespaceItem);
                RefreshQuickPanelSelections();
                UpdateQuickPanelState();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Account deletion failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateServerRegistrationPresentation(TrayMenuViewState viewState)
        {
            bool usersSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.Users;
            bool loginSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.Login;
            bool settingsUsersSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.SettingsUsers;
            bool settingsServersSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.SettingsServers;
            bool settingsActionSectionActive = settingsUsersSectionActive || settingsServersSectionActive;
            bool serversSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.Servers;
            bool credentialsSectionActive = viewState.SelectionLayout == TrayMenuSelectionLayout.CredentialSelectors;
            bool showInlineRegistration = serversSectionActive && _showInlineServerRegistration;
            bool isNewServerRegistration = _inlineServerRegistrationMode == InlineServerRegistrationMode.NewServer;
            bool isAuthorizedForActiveNamespace = _authenticationService.IsAuthorizedForActiveNamespace;
            bool canStartRegistration = _authenticationService.IsAuthorized && !_authenticationInProgress;
            bool canRegisterUser = usersSectionActive && !_authenticationService.IsAuthorized && !_authenticationInProgress;
            bool canLoginToActiveNamespace = !isAuthorizedForActiveNamespace && !_authenticationInProgress;
            bool canLogoutFromActiveNamespace = isAuthorizedForActiveNamespace && !_authenticationInProgress;

            _quickPanelUserRegistrationPanel.Visible = settingsUsersSectionActive && _showInlineUserRegistration;

            _quickPanelRegisterUserInlineButton.Visible = usersSectionActive && !credentialsSectionActive;
            _quickPanelRegisterUserInlineButton.Enabled = canRegisterUser;
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
            _quickPanelSelectionCard.Visible = true;
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
                _quickPanelRegisterUserInlineButton.Visible = false;
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
                    if (settingsUsersSectionActive && _showInlineUserRegistration)
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
                _quickPanelRegisterNewServerInlineButton.Text = "+ Register new server";
                _quickPanelRegisterNewServerInlineButton.Location = new Point(16, 14);
                _quickPanelRegisterNewServerInlineButton.Size = new Size(156, 28);
                _quickPanelServerRegistrationInlineButton.Text = "+ Sync already registered";
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
            _quickPanelDetailTitleLabel.Text = isNewServerRegistration ? "Register new server" : "Sync registered server";
            _quickPanelInsightCard.Location = new Point(32, 152);
            _quickPanelInsightCard.Size = new Size(578, 388);
            _quickPanelInsightTitleLabel.Location = new Point(92, 17);
            _quickPanelInsightTitleLabel.Text = string.Empty;
            _quickPanelInsightBodyLabel.Visible = false;
            _quickPanelRegisterNewServerInlineButton.Visible = false;
            _quickPanelServerRegistrationInlineButton.Visible = false;
            _quickPanelServerRegistrationUrlLabel.Location = new Point(16, 50);
            _quickPanelServerRegistrationUrlTextBox.Location = new Point(16, 70);
            _quickPanelServerRegistrationUrlTextBox.Size = isNewServerRegistration
                ? new Size(260, 25)
                : new Size(544, 25);
            _quickPanelServerRegistrationNameLabel.Location = new Point(300, 50);
            _quickPanelServerRegistrationNameTextBox.Location = new Point(300, 70);
            _quickPanelServerRegistrationNameTextBox.Size = new Size(260, 25);
            SetServerRegistrationControlsVisible(true, isNewServerRegistration);
            _quickPanelServerRegistrationBackButton.Location = new Point(16, 108);
            _quickPanelServerRegistrationSubmitButton.Location = new Point(
                _quickPanelServerRegistrationBackButton.Right + 5,
                _quickPanelServerRegistrationBackButton.Top);
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
            _quickPanelRegisterUserInlineButton.Visible = false;
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
                    _quickPanelCredentialGroupSettingsButton.Location = new Point(266, 36);
                    _quickPanelNamespaceLabel.Location = new Point(308, 14);
                    _quickPanelNamespaceComboBox.Location = new Point(308, 36);
                    _quickPanelNamespaceComboBox.Size = new Size(216, 25);
                    _quickPanelCredentialEntryCreateIconButton.Location = new Point(528, 36);
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
                else if (result.ExplicitlyInvalid)
                {
                    _authenticationService.RevokeAuthorization();
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

                ApplyCredentialSelectionCardMode(showGroupManager, showCreateGroup);
                _quickPanelCredentialEntryEditorPanel.Visible = !showGroupManager;
                _quickPanelCredentialGroupEditorPanel.Visible = showGroupManager;
                _quickPanelCredentialBackButton.Visible = showGroupManager || showCreateEntry || showCreateGroup;
                _quickPanelCredentialBackButton.Location = showGroupManager
                    ? new Point(144, 134)
                    : showCreateEntry
                        ? new Point(132, 303)
                        : new Point(16, 12);

                if (showGroupManager)
                {
                    _quickPanelCredentialGroupEditorPanel.BringToFront();
                    _quickPanelCredentialBackButton.BringToFront();
                }
                else
                {
                    _quickPanelCredentialEntryEditorPanel.BringToFront();

                    if (showCreateEntry)
                    {
                        _quickPanelCredentialBackButton.BringToFront();
                    }
                }

                _quickPanelCredentialSaveButton.Text = showCreateEntry ? "Save new item" : "Save changes";
                _quickPanelCredentialStatusLabel.Text = _credentialEditorStatusMessage;
                _quickPanelGroupStatusLabel.Text = _groupEditorStatusMessage;

                _quickPanelManagedGroupLabel.Visible = !showCreateGroup;
                _quickPanelManagedGroupComboBox.Visible = !showCreateGroup;
                _quickPanelManagedGroupNameLabel.Visible = !showCreateGroup;
                _quickPanelManagedGroupNameTextBox.Visible = !showCreateGroup;
                _quickPanelCredentialGroupCreateButton.Visible = false;
                _quickPanelNewGroupNameLabel.Visible = showCreateGroup;
                _quickPanelNewGroupNameTextBox.Visible = showCreateGroup;
                _quickPanelCredentialGroupSaveButton.Text = showCreateGroup ? "Save group" : "Save changes";

                if (showGroupManager)
                {
                    PopulateManagedGroupEditor();
                }
                else if (showCreateEntry)
                {
                    PopulateCredentialAssignedGroupSelector();
                }
                else
                {
                    QueueSelectedCredentialEditorLoad();
                }
            }
            finally
            {
                _updatingCredentialWorkspaceControls = false;
            }
        }

        private void ApplyCredentialSelectionCardMode(bool showGroupManager, bool showCreateGroup)
        {
            if (_trayMenuController?.ActiveSection != TrayMenuSection.Credentials)
            {
                return;
            }

            if (!showGroupManager)
            {
                _quickPanelUserLabel.Visible = true;
                _quickPanelUserComboBox.Visible = true;
                _quickPanelUserComboBox.Location = new Point(16, 36);
                _quickPanelUserComboBox.Size = new Size(244, 25);
                _quickPanelCredentialGroupSettingsButton.Visible = true;
                _quickPanelCredentialGroupSettingsButton.Location = new Point(266, 36);
                _quickPanelCredentialGroupSettingsButton.Text = "⚙";
                _quickPanelCredentialGroupSettingsButton.BackColor = Color.FromArgb(46, 48, 77);
                _quickPanelCredentialGroupSettingsButton.ForeColor = Color.White;
                _quickPanelNamespaceLabel.Visible = true;
                _quickPanelNamespaceComboBox.Visible = true;
                _quickPanelNamespaceLabel.Location = new Point(308, 14);
                _quickPanelNamespaceComboBox.Location = new Point(308, 36);
                _quickPanelNamespaceComboBox.Size = new Size(216, 25);
                _quickPanelCredentialEntryCreateIconButton.Visible = true;
                _quickPanelCredentialEntryCreateIconButton.Location = new Point(528, 36);
                _quickPanelCredentialEntryCreateIconButton.Text = "+";
                _quickPanelCredentialEntryCreateIconButton.BackColor = Color.FromArgb(46, 48, 77);
                _quickPanelCredentialEntryCreateIconButton.ForeColor = Color.White;
                _quickPanelCredentialEntryCreateIconButton.Enabled = true;
                return;
            }

            _quickPanelUserLabel.Visible = true;
            _quickPanelUserComboBox.Visible = true;
            _quickPanelUserComboBox.Location = new Point(16, 36);
            _quickPanelUserComboBox.Size = new Size(508, 25);
            _quickPanelCredentialGroupSettingsButton.Visible = false;
            _quickPanelNamespaceLabel.Visible = false;
            _quickPanelNamespaceComboBox.Visible = false;
            _quickPanelCredentialEntryCreateIconButton.Visible = true;
            _quickPanelCredentialEntryCreateIconButton.Location = new Point(528, 36);
            _quickPanelCredentialEntryCreateIconButton.Text = "+";
            _quickPanelCredentialEntryCreateIconButton.BackColor = Color.FromArgb(120, 205, 0);
            _quickPanelCredentialEntryCreateIconButton.ForeColor = Color.Black;
            _quickPanelCredentialEntryCreateIconButton.Enabled = !_credentialsLoading && !showCreateGroup;
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
                _quickPanelCredentialStatusLabel.Text = "Loading selected credential...";
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
                _quickPanelGroupStatusLabel.Text = _credentialGroups.Count == 0 && _credentialWorkspaceMode != CredentialWorkspaceMode.CreateGroup
                    ? "No groups returned for the active server."
                    : "Manage the same credential groups that the extension workspace uses.";
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

            if (_authenticationService.IsAuthorizedForActiveNamespace)
            {
                if (_trayMenuController?.ActiveSection == TrayMenuSection.Servers)
                {
                    try
                    {
                        _authenticationInProgress = true;
                        UpdateAuthenticationMenuItems();
                        ShowServerAuthenticationProgress();
                        await _authenticationService.LogoutActiveNamespaceAsync().ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Logout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

                    return;
                }

                logoutToolStripMenuItem_Click(sender!, EventArgs.Empty);
                return;
            }

            loginToolStripMenuItem_Click(sender, EventArgs.Empty);
        }

        private void quickPanelSecondaryActionButton_Click(object? sender, EventArgs e)
        {
            if (_trayMenuController?.ActiveSection == TrayMenuSection.Users)
            {
                ShowBaseUrlPrompt(showAsTrayPrompt: true);
                return;
            }

            _trayMenuController?.SetActiveSection(TrayMenuSection.Servers, triggerStateChanged: true);
            quickPanelRegisterNewServerInlineButton_Click(sender, EventArgs.Empty);
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
            UpdateQuickPanelState();
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
            UpdateQuickPanelState();
        }

        private void PopulateSettingsUserActions()
        {
            _quickPanelSettingsUserActionComboBox.Items.Clear();
            _quickPanelSettingsUserActionComboBox.Items.Add(new SelectionOption("Select account action", string.Empty));
            _quickPanelSettingsUserActionComboBox.Items.Add(new SelectionOption("Create new user account", "create"));
            _quickPanelSettingsUserActionComboBox.Items.Add(new SelectionOption("Delete user-account", "delete"));
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
            }
        }

        private void PopulateSettingsServerActions()
        {
            _quickPanelSettingsServerActionComboBox.Items.Clear();
            _quickPanelSettingsServerActionComboBox.Items.Add(new SelectionOption("Select server action", string.Empty));
            _quickPanelSettingsServerActionComboBox.Items.Add(new SelectionOption("Register new server", "register"));
            _quickPanelSettingsServerActionComboBox.Items.Add(new SelectionOption("Sync already registered server", "sync"));
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
                _trayMenuController.SetActiveSection(TrayMenuSection.Servers, triggerStateChanged: true);
                ShowInlineServerRegistration(InlineServerRegistrationMode.NewServer);
                return;
            }

            if (option.Value == "sync")
            {
                _trayMenuController.SetActiveSection(TrayMenuSection.Servers, triggerStateChanged: true);
                ShowInlineServerRegistration(InlineServerRegistrationMode.NamespaceSync);
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
            SetCredentialWorkspaceMode(CredentialWorkspaceMode.ViewEntry);
        }

        private async void quickPanelCredentialSaveButton_Click(object? sender, EventArgs e)
        {
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

        private async void quickPanelCredentialGroupSaveButton_Click(object? sender, EventArgs e)
        {
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

                    _credentialWorkspaceMode = CredentialWorkspaceMode.ManageGroup;
                    _groupEditorStatusMessage = "Group created.";
                    _selectedManagedGroupId = createdGroup?.GroupId ?? _selectedManagedGroupId;
                    await ReloadCredentialWorkspaceAsync().ConfigureAwait(true);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_selectedManagedGroupId))
                {
                    _groupEditorStatusMessage = "Please select a group first.";
                    return;
                }

                string groupName = _quickPanelManagedGroupNameTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    _groupEditorStatusMessage = "Please enter a group name.";
                    return;
                }

                await _credentialCollectionService
                    .UpdateGroupAsync(_selectedManagedGroupId, groupName)
                    .ConfigureAwait(true);

                _groupEditorStatusMessage = "Group updated.";
                await ReloadCredentialWorkspaceAsync().ConfigureAwait(true);
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
