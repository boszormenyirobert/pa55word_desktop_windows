using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;

namespace TrayApp
{
    public partial class Form1 : Form
    {
        private enum RegistrationPromptMode
        {
            DeviceRegistration,
            ServerRegistration
        }

        private bool _isExitRequested = false;
        private bool _isDeviceRegistered;
        private RegistrationPromptMode _registrationPromptMode = RegistrationPromptMode.DeviceRegistration;
        private readonly BootstrapRegistrationService _bootstrapRegistrationService = new BootstrapRegistrationService();
        private readonly RegisteredDeviceService _registeredDeviceService = new RegisteredDeviceService();
        private readonly HttpApiClient _httpApiClient = new HttpApiClient();
        private readonly AuthorizationSessionStore _authorizationSessionStore = new AuthorizationSessionStore();
        private readonly ExtensionSessionStateStore _extensionSessionStateStore = new ExtensionSessionStateStore();
        private readonly AuthenticationService _authenticationService;
        private readonly AuthStateMonitorService _authStateMonitorService;
        private readonly NativePipeMessageServer _nativePipeMessageServer;
        private readonly EventWaitHandle _activateInstanceEvent;
        private readonly CancellationTokenSource _activateInstanceCts = new();
        private readonly ToolStripMenuItem _userSelectionToolStripMenuItem = new("user selection");
        private readonly ToolStripMenuItem _namespaceSelectionToolStripMenuItem = new("server selection");
        private readonly ToolStripMenuItem _newUserToolStripMenuItem = new("new user");
        private readonly UserSelectionMenuController _userSelectionMenuController;
        private readonly NamespaceSelectionMenuController _namespaceSelectionMenuController;
        private bool _nativePipeServerStarted;
        private bool _authenticationInProgress;
        private Task? _activateInstanceTask;

        public Form1()
        {
            InitializeComponent();

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

            _authStateMonitorService = new AuthStateMonitorService(
                _authenticationService,
                _extensionSessionStateStore,
                message => Debug.WriteLine(message));

            var commandHandler = new BrowserCommandHandler(
                _registeredDeviceService,
                _httpApiClient,
                _authenticationService,
                _extensionSessionStateStore,
                message => Debug.WriteLine(message));

            _nativePipeMessageServer = new NativePipeMessageServer(
                commandHandler,
                message => Debug.WriteLine(message));

            _activateInstanceTask = Task.Run(() => ListenForActivationRequestsAsync(_activateInstanceCts.Token));

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
            contextMenuStrip1.Items.Insert(0, _namespaceSelectionToolStripMenuItem);
            contextMenuStrip1.Items.Insert(0, _userSelectionToolStripMenuItem);
            contextMenuStrip1.Items.Insert(3, _newUserToolStripMenuItem);
            contextMenuStrip1.Opening += contextMenuStrip1_Opening;
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
                    EnterRegisteredMode();
                }
                else
                {
                    ShowBaseUrlPrompt();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bootstrap registration failed: {ex}");
                ShowBaseUrlPrompt();
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

            EnterRegisteredMode();
        }

        private void OnRegistrationFailed(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(OnRegistrationFailed), message);
                return;
            }

            if (_registrationPromptMode == RegistrationPromptMode.ServerRegistration)
            {
                ShowServerRegistrationPrompt();
            }
            else
            {
                ShowBaseUrlPrompt();
            }

            MessageBox.Show(message, "Registration", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void HideToTray()
        {
            Debug.WriteLine("TRAY MODE");
            Hide();
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
        }

        private void EnterRegisteredMode()
        {
            _isDeviceRegistered = true;
            EnsureRegisteredServicesStarted();
            UpdateAuthenticationMenuItems();
            HideToTray();
        }

        private void ShowBaseUrlPrompt()
        {
            bool hasRegisteredUsers = _registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) &&
                registrations != null &&
                registrations.Count > 0;

            _registrationPromptMode = RegistrationPromptMode.DeviceRegistration;
            _isDeviceRegistered = false;
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

            ShowAndActivateWindow();
            baseUrlTextBox.Focus();
        }

        private void ShowServerRegistrationPrompt()
        {
            _registrationPromptMode = RegistrationPromptMode.ServerRegistration;
            pictureBoxQr.Visible = false;
            baseUrlLabel.Visible = true;
            baseUrlTextBox.Visible = true;
            namespaceNameLabel.Visible = true;
            namespaceNameTextBox.Visible = true;
            continueRegistrationButton.Visible = true;
            baseUrlLabel.Text = "Namespace url";
            namespaceNameLabel.Text = "Namespace name";
            continueRegistrationButton.Text = "Generate QR";
            device_registration.Text = "Server registration";

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

            ShowAndActivateWindow();
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

                    BeginInvoke(new Action(ShowAndActivateWindow));
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

        private void ShowAndActivateWindow()
        {
            if (IsDisposed)
            {
                return;
            }

            Show();
            ShowInTaskbar = true;

            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            BringToFront();
            Activate();
        }

        private void HandleAuthorizationNotificationChanged(string? message, ToolTipIcon icon)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string?, ToolTipIcon>(HandleAuthorizationNotificationChanged), message, icon);
                return;
            }

            UpdateAuthenticationMenuItems();

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

        private void UpdateAuthenticationMenuItems()
        {
            bool showAuthenticationActions = _isDeviceRegistered;
            bool isAuthorized = _authenticationService.IsAuthorized;
            bool canLogin = showAuthenticationActions && !isAuthorized;
            bool canLogout = showAuthenticationActions && isAuthorized;

            loginToolStripMenuItem.Visible = canLogin;
            loginToolStripMenuItem.Enabled = canLogin && !_authenticationInProgress;
            logoutToolStripMenuItem.Visible = canLogout;
            logoutToolStripMenuItem.Enabled = canLogout && !_authenticationInProgress;
            registerServerToolStripMenuItem.Visible = canLogout;
            registerServerToolStripMenuItem.Enabled = canLogout && !_authenticationInProgress;
            _newUserToolStripMenuItem.Visible = showAuthenticationActions;
            _newUserToolStripMenuItem.Enabled = showAuthenticationActions && !_authenticationInProgress;
        }

        private void contextMenuStrip1_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateAuthenticationMenuItems();
            _userSelectionMenuController.RefreshMenu();
            _namespaceSelectionMenuController.RefreshMenu();
        }

        private async void continueRegistrationButton_Click(object sender, EventArgs e)
        {
            try
            {
                continueRegistrationButton.Enabled = false;

                if (_registrationPromptMode == RegistrationPromptMode.ServerRegistration)
                {
                    await _bootstrapRegistrationService
                        .PrepareServerRegistrationQrAsync(baseUrlTextBox.Text, namespaceNameTextBox.Text)
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
                string? pollingBaseUrl = _registrationPromptMode == RegistrationPromptMode.ServerRegistration
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
            _activateInstanceCts.Cancel();
            _activateInstanceEvent.Dispose();
            _activateInstanceCts.Dispose();
            _nativePipeMessageServer.Dispose();
            _authStateMonitorService.Dispose();
            base.OnFormClosing(e);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
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

        private void newUserToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            ShowBaseUrlPrompt();
        }

        private async void loginToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                _authenticationInProgress = true;
                UpdateAuthenticationMenuItems();

                bool authorized = await _authenticationService.EnsureAuthorizedAsync(allowInteractiveLogin: true).ConfigureAwait(true);
                if (!authorized)
                {
                    _authenticationService.RevokeAuthorization();
                    _extensionSessionStateStore.MarkInvalid("authorization_declined");
                    _authenticationInProgress = false;
                    UpdateAuthenticationMenuItems();
                    return;
                }

                _extensionSessionStateStore.MarkAuthorized();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Login", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _authenticationInProgress = false;
                UpdateAuthenticationMenuItems();
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAndActivateWindow();
        }
    }
}