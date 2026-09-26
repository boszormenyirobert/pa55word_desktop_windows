using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;

namespace TrayApp
{
    public partial class Form1 : Form
    {
        private enum RegistrationPromptMode
        {
            DeviceRegistration,
            NewServerRegistration,
            ServerRegistration
        }

        private bool _isExitRequested = false;
        private bool _isDeviceRegistered;
        private RegistrationPromptMode _registrationPromptMode = RegistrationPromptMode.DeviceRegistration;
        private readonly BootstrapRegistrationService _bootstrapRegistrationService = new BootstrapRegistrationService();
        private readonly RegisteredDeviceService _registeredDeviceService = new RegisteredDeviceService();
        private readonly HttpApiClient _httpApiClient = new HttpApiClient();
        private readonly AccountLifecycleService _accountLifecycleService;
        private readonly AuthorizationSessionStore _authorizationSessionStore = new AuthorizationSessionStore();
        private readonly ExtensionSessionStateStore _extensionSessionStateStore = new ExtensionSessionStateStore();
        private readonly AuthenticationService _authenticationService;
        private readonly AuthStateMonitorService _authStateMonitorService;
        private readonly NativePipeMessageServer _nativePipeMessageServer;
        private readonly EventWaitHandle _activateInstanceEvent;
        private readonly CancellationTokenSource _activateInstanceCts = new();
        private bool _nativePipeServerStarted;
        private bool _authenticationInProgress;
        private Task? _activateInstanceTask;
        private bool _registrationHostedInTray;

        public Form1()
        {
            InitializeComponent();
            Opacity = 0;
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;

            _activateInstanceEvent = new EventWaitHandle(
                initialState: false,
                mode: EventResetMode.AutoReset,
                name: Program.ActivateInstanceEventName);

            _authenticationService = new AuthenticationService(
                _registeredDeviceService,
                _httpApiClient,
                _authorizationSessionStore,
                HandleAuthorizationNotificationChanged,
                message => Debug.WriteLine(message));

            _accountLifecycleService = new AccountLifecycleService(_httpApiClient);

            _authStateMonitorService = new AuthStateMonitorService(
                _authenticationService,
                _extensionSessionStateStore,
                HandleAuthorizationStateEvaluated,
                message => Debug.WriteLine(message));

            _extensionSessionStateStore.StateChanged += HandleExtensionSessionStateChanged;

            _credentialCollectionService = new CredentialCollectionService(
                _registeredDeviceService,
                _httpApiClient,
                _authenticationService,
                _extensionSessionStateStore,
                message => Debug.WriteLine(message));

            var commandHandler = new BrowserCommandHandler(
                _registeredDeviceService,
                _httpApiClient,
                _authenticationService,
                _extensionSessionStateStore,
                message =>
                {
                    Debug.WriteLine(message);
                    DiagnosticLog.Write(message);
                });

            _nativePipeMessageServer = new NativePipeMessageServer(
                commandHandler,
                message =>
                {
                    Debug.WriteLine(message);
                    DiagnosticLog.Write(message);
                });

            _activateInstanceTask = Task.Run(() => ListenForActivationRequestsAsync(_activateInstanceCts.Token));
            _registrationHostedInTray = false;

            notifyIcon1.ContextMenuStrip = null;
            notifyIcon1.Visible = true;
            notifyIcon1.Text = "ZeroIntrusion Agent";

            _namespaceSelectionMenuController = new NamespaceSelectionMenuController(
                _registeredDeviceService,
                _namespaceSelectionToolStripMenuItem);

            _userSelectionMenuController = new UserSelectionMenuController(
                _registeredDeviceService,
                _userSelectionToolStripMenuItem,
                () => _namespaceSelectionMenuController.RefreshMenu(),
                () => _authenticationService.IsAuthorized);

            _userSelectionToolStripMenuItem.Visible = false;
            loginToolStripMenuItem.Click += loginToolStripMenuItem_Click;
            _namespaceSelectionToolStripMenuItem.Visible = false;
            _newUserToolStripMenuItem.Visible = false;
            _newUserToolStripMenuItem.Click += newUserToolStripMenuItem_Click;
            _openTrayPanelToolStripMenuItem.Click += openToolStripMenuItem_Click;
            contextMenuStrip1.Items.Insert(0, _namespaceSelectionToolStripMenuItem);
            contextMenuStrip1.Items.Insert(0, _userSelectionToolStripMenuItem);
            contextMenuStrip1.Items.Insert(0, _openTrayPanelToolStripMenuItem);
            contextMenuStrip1.Opening += contextMenuStrip1_Opening;
            contextMenuStrip1.Closing += contextMenuStrip1_Closing;
            contextMenuStrip1.Closed += contextMenuStrip1_Closed;
            InitializeTrayShell();
            UpdateAuthenticationMenuItems();

            _bootstrapRegistrationService.RegistrationCompleted += OnRegistrationCompleted;
            _bootstrapRegistrationService.RegistrationFailed += OnRegistrationFailed;

            BootstrapRegistration();
        }

        private async void BootstrapRegistration()
        {
            try
            {
                bool deviceIsRegistered = await _bootstrapRegistrationService.LoadOrCreateConfig();
                Debug.WriteLine($"Registration state: {deviceIsRegistered}");

                if (deviceIsRegistered)
                {
                    await _accountLifecycleService.RemoveDeletedAccountsAsync();
                    deviceIsRegistered = _registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) &&
                        registrations is { Count: > 0 };

                    if (deviceIsRegistered)
                    {
                        EnterRegisteredMode();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bootstrap registration failed: {ex}");
            }
        }

        private async Task StartPollingAfterRenderAsync(string? baseUrlOverride = null)
        {
            _bootstrapRegistrationService.StartRegistrationPolling(baseUrlOverride);
            await Task.CompletedTask;
        }

        private void EnsureRegisteredServicesStarted()
        {
            if (_nativePipeServerStarted)
            {
                return;
            }

            _nativePipeMessageServer.Start();
            _authStateMonitorService.Start();
            _nativePipeServerStarted = true;
        }

        private void OnRegistrationCompleted()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(OnRegistrationCompleted));
                return;
            }

            if (_showInlineServerRegistration &&
                _registrationPromptMode is RegistrationPromptMode.ServerRegistration or RegistrationPromptMode.NewServerRegistration)
            {
                _lastTrayMessage = _registrationPromptMode == RegistrationPromptMode.NewServerRegistration
                    ? "New server registration is complete."
                    : "Namespace sync is complete.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                ResetInlineServerRegistrationState(clearInputs: true);
                RefreshQuickPanelSelections();
                UpdateQuickPanelState();
                UpdateAuthenticationMenuItems();
                return;
            }

            EnterRegisteredMode();
        }

        private void OnRegistrationFailed(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(OnRegistrationFailed), message);
                return;
            }

            if (_showInlineServerRegistration &&
                _registrationPromptMode is RegistrationPromptMode.ServerRegistration or RegistrationPromptMode.NewServerRegistration)
            {
                _inlineServerRegistrationSubmitting = false;
                _inlineServerRegistrationAwaitingCompletion = false;
                _serverRegistrationStatusMessage = message;
                UpdateQuickPanelState();
                return;
            }

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
                ShowBaseUrlPrompt(_registrationHostedInTray);
            }

            MessageBox.Show(message, "Registration", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void HideToTray()
        {
            Debug.WriteLine("TRAY MODE");
            CloseQuickPanel();
            Hide();
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
        }

        private void EnterRegisteredMode()
        {
            bool showRegisteredTrayView = _registrationHostedInTray;
            _registrationHostedInTray = false;
            _isDeviceRegistered = true;
            EnsureRegisteredServicesStarted();
            _trayMenuController?.SetActiveSection(TrayMenuSection.Overview);
            _lastTrayMessage = "Desktop registration is complete.";
            _lastTrayMessageIcon = ToolTipIcon.Info;
            _lastTrayStateUpdatedAt = DateTimeOffset.Now;
            RefreshQuickPanelSelections();
            UpdateQuickPanelState();
            UpdateAuthenticationMenuItems();

            if (showRegisteredTrayView)
            {
                HideToTray();
                ShowQuickPanel(refreshFromBackend: false);
                return;
            }

            HideToTray();
        }

        private void ShowBaseUrlPrompt(bool showAsTrayPrompt = false)
        {
            bool hasRegisteredUsers = _registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) &&
                registrations != null &&
                registrations.Count > 0;

            _registrationPromptMode = RegistrationPromptMode.DeviceRegistration;
            _registrationHostedInTray = showAsTrayPrompt;
            _isDeviceRegistered = false;
            ApplyRegistrationLayout();
            pictureBoxQr.Visible = false;
            namespaceNameLabel.Visible = true;
            namespaceNameTextBox.Visible = true;
            namespaceNameTextBox.Text = string.Empty;
            baseUrlLabel.Visible = true;
            baseUrlTextBox.Visible = true;
            continueRegistrationButton.Visible = true;
            baseUrlLabel.Text = "Base url";
            namespaceNameLabel.Text = "User name";
            continueRegistrationButton.Text = "Continue";
            device_registration.Text = "Device registration";
            UpdateAuthenticationMenuItems();

            try
            {
                baseUrlTextBox.Text = hasRegisteredUsers
                    ? AppEnvironment.BackendBaseUrl
                    : _bootstrapRegistrationService.GetBootstrapBaseUrl();
            }
            catch
            {
                baseUrlTextBox.Text = _bootstrapRegistrationService.GetBootstrapBaseUrl();
            }

            ShowAndActivateWindow(showAsQuickPanel: showAsTrayPrompt);
            baseUrlTextBox.Focus();
        }

        private void ShowServerRegistrationPrompt()
        {
            _registrationPromptMode = RegistrationPromptMode.NewServerRegistration;
            _registrationHostedInTray = false;
            ApplyRegistrationLayout();
            pictureBoxQr.Visible = false;
            baseUrlLabel.Visible = true;
            baseUrlTextBox.Visible = true;
            namespaceNameLabel.Visible = true;
            namespaceNameTextBox.Visible = true;
            continueRegistrationButton.Visible = true;
            baseUrlLabel.Text = "Namespace url";
            namespaceNameLabel.Text = "Namespace name";
            continueRegistrationButton.Text = "Generate QR";
            device_registration.Text = "Register new server";

            try
            {
                baseUrlTextBox.Text = AppEnvironment.BackendBaseUrl;
            }
            catch
            {
                baseUrlTextBox.Text = string.Empty;
            }

            namespaceNameTextBox.Text = string.Empty;

            ShowAndActivateWindow();
            baseUrlTextBox.Focus();
        }

        private void ShowNamespaceSyncPrompt()
        {
            _registrationPromptMode = RegistrationPromptMode.ServerRegistration;
            _registrationHostedInTray = false;
            ApplyRegistrationLayout();
            pictureBoxQr.Visible = false;
            baseUrlLabel.Visible = true;
            baseUrlTextBox.Visible = true;
            namespaceNameLabel.Visible = false;
            namespaceNameTextBox.Visible = false;
            continueRegistrationButton.Visible = true;
            baseUrlLabel.Text = "Namespace url";
            continueRegistrationButton.Text = "Generate QR";
            device_registration.Text = "Namespace sync";

            try
            {
                baseUrlTextBox.Text = AppEnvironment.BackendBaseUrl;
            }
            catch
            {
                baseUrlTextBox.Text = string.Empty;
            }

            namespaceNameTextBox.Text = string.Empty;

            ShowAndActivateWindow();
            baseUrlTextBox.Focus();
        }

        private void ShowRegistrationQr()
        {
            ApplyRegistrationLayout();
            baseUrlLabel.Visible = false;
            baseUrlTextBox.Visible = false;
            namespaceNameLabel.Visible = false;
            namespaceNameTextBox.Visible = false;
            continueRegistrationButton.Visible = false;
            pictureBoxQr.Visible = true;

            Bitmap? qrCode = AppServices.Qr.GetQrCode();

            if (qrCode == null)
            {
                MessageBox.Show("QR code is empty");
                return;
            }

            pictureBoxQr.Image = qrCode;
            pictureBoxQr.SizeMode = PictureBoxSizeMode.Zoom;

            ShowAndActivateWindow(showAsQuickPanel: _registrationHostedInTray);
        }

        private async Task ListenForActivationRequestsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Run(() => _activateInstanceEvent.WaitOne(), cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    BeginInvoke(new Action(() =>
                    {
                        if (_isDeviceRegistered)
                        {
                            ShowQuickPanel(refreshFromBackend: false);
                        }
                        else
                        {
                            ShowBaseUrlPrompt(showAsTrayPrompt: true);
                        }
                    }));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private void ShowAndActivateWindow(bool showAsQuickPanel = false)
        {
            if (IsDisposed)
            {
                return;
            }

            if (Opacity <= 0)
            {
                Opacity = 1;
            }

            Show();
            ShowInTaskbar = !showAsQuickPanel;

            if (showAsQuickPanel)
            {
                PositionQuickPanel();
            }

            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            BringToFront();
            Activate();
        }

        private void PositionQuickPanel()
        {
            StartPosition = FormStartPosition.Manual;
            Location = ResolveTrayAnchoredScreenPoint(_quickPanelContainer.Size);
        }

        private void HandleAuthorizationNotificationChanged(string? message, ToolTipIcon icon)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string?, ToolTipIcon>(HandleAuthorizationNotificationChanged), message, icon);
                return;
            }

            _lastTrayMessage = message;
            _lastTrayMessageIcon = icon;
            _lastTrayStateUpdatedAt = DateTimeOffset.Now;
            UpdateAuthenticationMenuItems();

            if (_authenticationInProgress && _trayMenuController?.ActiveSection == TrayMenuSection.Servers)
            {
                ShowServerAuthenticationProgress();
            }
            else
            {
                UpdateQuickPanelState();
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            notifyIcon1.BalloonTipTitle = "ZeroIntrusion";
            notifyIcon1.BalloonTipText = message;
            notifyIcon1.BalloonTipIcon = icon;
            notifyIcon1.Visible = true;
            notifyIcon1.ShowBalloonTip(5000);
        }

        private void HandleExtensionSessionStateChanged(ExtensionSessionStateResponseDto state)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<ExtensionSessionStateResponseDto>(HandleExtensionSessionStateChanged), state);
                return;
            }

            if (!state.Authorized && state.CloseExtension)
            {
                _authenticationService.RevokeAuthorization();
            }

            _lastTrayStateUpdatedAt = DateTimeOffset.Now;
            UpdateAuthenticationMenuItems();
            UpdateQuickPanelState();

            if (_isDeviceRegistered)
            {
                _ = RefreshQuickPanelFromBackendAsync();
            }
        }

        private void HandleAuthorizationStateEvaluated()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(HandleAuthorizationStateEvaluated));
                return;
            }

            _lastTrayStateUpdatedAt = DateTimeOffset.Now;
            UpdateAuthenticationMenuItems();
            UpdateQuickPanelState();
        }

        private void UpdateAuthenticationMenuItems()
        {
            bool showAuthenticationActions = _isDeviceRegistered;
            bool isAuthorized = _authenticationService.IsAuthorized;
            bool isAuthorizedForActiveNamespace = _authenticationService.IsAuthorizedForActiveNamespace;
            bool canLogin = showAuthenticationActions && !isAuthorizedForActiveNamespace;
            bool canLogout = showAuthenticationActions && isAuthorized;
            bool usersSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.Users;
            bool serversSectionActive = _trayMenuController?.ActiveSection == TrayMenuSection.Servers;
            bool expandedTrayView = _showExpandedTrayMenu && _isDeviceRegistered;
            bool hasUserSelectionItems = _userSelectionToolStripMenuItem.DropDownItems.Count > 0;
            bool hasNamespaceSelectionItems = _namespaceSelectionToolStripMenuItem.DropDownItems.Count > 0;

            loginToolStripMenuItem.Visible = !expandedTrayView && canLogin;
            loginToolStripMenuItem.Enabled = canLogin && !_authenticationInProgress;
            logoutToolStripMenuItem.Visible = !expandedTrayView && canLogout;
            logoutToolStripMenuItem.Enabled = canLogout && !_authenticationInProgress;
            _openTrayPanelToolStripMenuItem.Visible = !expandedTrayView && !_isDeviceRegistered;
            _openTrayPanelToolStripMenuItem.Text = "open registration";
            _userSelectionToolStripMenuItem.Visible = !expandedTrayView && !isAuthorized && hasUserSelectionItems;
            _namespaceSelectionToolStripMenuItem.Visible = !expandedTrayView && hasNamespaceSelectionItems;
            _quickPanelPrimaryActionButton.Text = canLogin ? "Login" : "Logout";
            _quickPanelPrimaryActionButton.Visible = !serversSectionActive;
            _quickPanelPrimaryActionButton.Enabled = (canLogin || canLogout) && !_authenticationInProgress;
            _quickPanelRegisterServerButton.Visible = serversSectionActive;
            _quickPanelRegisterServerButton.Enabled = !_authenticationInProgress &&
                serversSectionActive && canLogout;
            _quickPanelRegisterServerButton.Text = "Register server";
            _quickPanelRefreshButton.Enabled = showAuthenticationActions && !_quickPanelRefreshInProgress;

            if (canLogin && !_authenticationInProgress)
            {
                _quickPanelPrimaryActionButton.BackColor = Color.FromArgb(120, 205, 0);
                _quickPanelPrimaryActionButton.ForeColor = Color.FromArgb(24, 30, 24);
                _quickPanelPrimaryActionButton.FlatAppearance.BorderColor = Color.FromArgb(120, 205, 0);
            }
            else if (canLogout && !_authenticationInProgress)
            {
                _quickPanelPrimaryActionButton.BackColor = Color.FromArgb(214, 88, 88);
                _quickPanelPrimaryActionButton.ForeColor = Color.White;
                _quickPanelPrimaryActionButton.FlatAppearance.BorderColor = Color.FromArgb(214, 88, 88);
            }
            else
            {
                _quickPanelPrimaryActionButton.BackColor = Color.FromArgb(46, 48, 77);
                _quickPanelPrimaryActionButton.ForeColor = Color.White;
                _quickPanelPrimaryActionButton.FlatAppearance.BorderColor = Color.FromArgb(46, 48, 77);
            }
        }


        private async void continueRegistrationButton_Click(object sender, EventArgs e)
        {
            try
            {
                continueRegistrationButton.Enabled = false;

                if (_registrationPromptMode == RegistrationPromptMode.ServerRegistration)
                {
                    await _bootstrapRegistrationService
                        .PrepareServerRegistrationQrAsync(baseUrlTextBox.Text)
                        .ConfigureAwait(true);
                }
                else if (_registrationPromptMode == RegistrationPromptMode.NewServerRegistration)
                {
                    await _bootstrapRegistrationService
                        .PrepareNewServerRegistrationQrAsync(baseUrlTextBox.Text, namespaceNameTextBox.Text)
                        .ConfigureAwait(true);
                }
                else
                {
                    await _bootstrapRegistrationService
                        .PrepareRegistrationQrAsync(baseUrlTextBox.Text, namespaceNameTextBox.Text)
                        .ConfigureAwait(true);
                }

                Debug.WriteLine("SHOW QR");
                ShowRegistrationQr();
                _lastTrayMessage = "Registration QR generated. Waiting for mobile confirmation.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                string? pollingBaseUrl = _registrationPromptMode is RegistrationPromptMode.ServerRegistration or RegistrationPromptMode.NewServerRegistration
                    ? baseUrlTextBox.Text
                    : null;
                _ = StartPollingAfterRenderAsync(pollingBaseUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Registration", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                continueRegistrationButton.Enabled = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isExitRequested)
            {
                e.Cancel = true;
                HideToTray();

                return;
            }

            try
            {
                _authenticationService.LogoutAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH] Graceful shutdown revoke failed: {ex.Message}");
            }

            notifyIcon1.Visible = false;
            CloseQuickPanel();
            _activateInstanceCts.Cancel();
            _activateInstanceEvent.Dispose();
            _activateInstanceCts.Dispose();
            _nativePipeMessageServer.Dispose();
            _authStateMonitorService.Dispose();
            base.OnFormClosing(e);
        }

        private void exitToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            _isExitRequested = true;
            notifyIcon1.Visible = false;
            Close();
        }

        private async void logoutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                _authenticationInProgress = true;
                UpdateAuthenticationMenuItems();
                await _authenticationService.LogoutAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Logout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _authenticationInProgress = false;
                _extensionSessionStateStore.MarkInvalid("authorization_revoked");
                UpdateAuthenticationMenuItems();
            }
        }

        private void registerServerToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (!_isDeviceRegistered)
            {
                return;
            }

            ShowServerRegistrationPrompt();
        }

        private async void loginToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                _authenticationInProgress = true;
                _lastTrayMessage = "Waiting for confirmation on your mobile device.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;

                if (_trayMenuController?.ActiveSection == TrayMenuSection.Servers)
                {
                    ShowServerAuthenticationProgress();
                }
                else
                {
                    UpdateQuickPanelState();
                }

                UpdateAuthenticationMenuItems();

                bool authorized = await _authenticationService.EnsureAuthorizedForActiveNamespaceAsync(allowInteractiveLogin: true).ConfigureAwait(true);
                if (!authorized)
                {
                    _authenticationService.RevokeAuthorization();
                    _extensionSessionStateStore.MarkInvalid("authorization_declined");
                    _authenticationInProgress = false;
                    UpdateAuthenticationMenuItems();
                    return;
                }

                _extensionSessionStateStore.MarkAuthorized();
                _lastTrayMessage = "Desktop attestation is active.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Login", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _authenticationInProgress = false;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                UpdateQuickPanelState();
                UpdateAuthenticationMenuItems();
            }
        }

    }
}