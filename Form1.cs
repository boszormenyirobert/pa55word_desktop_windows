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
        private bool _isExitRequested = false;
        private bool _isDeviceRegistered;
        private readonly BootstrapRegistrationService _bootstrapRegistrationService = new BootstrapRegistrationService();
        private readonly RegisteredDeviceService _registeredDeviceService = new RegisteredDeviceService();
        private readonly HttpApiClient _httpApiClient = new HttpApiClient();
        private readonly AuthorizationSessionStore _authorizationSessionStore = new AuthorizationSessionStore();
        private readonly AuthenticationService _authenticationService;
        private readonly NativePipeMessageServer _nativePipeMessageServer;
        private readonly EventWaitHandle _activateInstanceEvent;
        private readonly CancellationTokenSource _activateInstanceCts = new();
        private readonly ToolStripMenuItem _namespaceSelectionToolStripMenuItem = new("namespace selection");
        private readonly NamespaceSelectionMenuController _namespaceSelectionMenuController;
        private bool _nativePipeServerStarted;
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
                HandleAuthorizationNotificationChanged);

            var commandHandler = new BrowserCommandHandler(
                _registeredDeviceService,
                _httpApiClient,
                _authenticationService,
                message => Debug.WriteLine(message));

            _nativePipeMessageServer = new NativePipeMessageServer(
                commandHandler,
                message => Debug.WriteLine(message));

            _activateInstanceTask = Task.Run(() => ListenForActivationRequestsAsync(_activateInstanceCts.Token));

            notifyIcon1.Visible = true;
            notifyIcon1.Text = "ZeroIntrusion Agent";
            ShowStartupNotification();

            _namespaceSelectionMenuController = new NamespaceSelectionMenuController(
                _registeredDeviceService,
                _namespaceSelectionToolStripMenuItem);

            loginToolStripMenuItem.Click += loginToolStripMenuItem_Click;
            _namespaceSelectionToolStripMenuItem.Visible = false;
            contextMenuStrip1.Items.Insert(0, _namespaceSelectionToolStripMenuItem);
            contextMenuStrip1.Opening += contextMenuStrip1_Opening;
            UpdateAuthenticationMenuItems();

            _bootstrapRegistrationService.RegistrationCompleted += OnRegistrationCompleted;
            _bootstrapRegistrationService.RegistrationFailed += OnRegistrationFailed;

            BootstrapRegistration();
        }

        private void ShowStartupNotification()
        {
            notifyIcon1.ShowBalloonTip(
                100,
                "ZeroIntrusion",
                "Application started and is running in the tray.",
                ToolTipIcon.Info);
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

        private async Task StartPollingAfterRenderAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            _bootstrapRegistrationService.StartRegistrationPolling();
        }

        private void EnsureRegisteredServicesStarted()
        {
            if (_nativePipeServerStarted)
            {
                return;
            }

            _nativePipeMessageServer.Start();
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

            ShowBaseUrlPrompt();
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
            _isDeviceRegistered = false;
            pictureBoxQr.Visible = false;
            baseUrlLabel.Visible = true;
            baseUrlTextBox.Visible = true;
            continueRegistrationButton.Visible = true;
            UpdateAuthenticationMenuItems();

            baseUrlTextBox.Text = _bootstrapRegistrationService.GetBootstrapBaseUrl();

            ShowAndActivateWindow();
            baseUrlTextBox.Focus();
        }

        private void ShowRegistrationQr()
        {
            baseUrlLabel.Visible = false;
            baseUrlTextBox.Visible = false;
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

        private void HandleAuthorizationNotificationChanged(string? message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string?>(HandleAuthorizationNotificationChanged), message);
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            notifyIcon1.ShowBalloonTip(
                3000,
                "ZeroIntrusion",
                message,
                ToolTipIcon.Info);
        }

        private void UpdateAuthenticationMenuItems()
        {
            bool showAuthenticationActions = _isDeviceRegistered;
            bool isAuthorized = _authorizationSessionStore.IsAuthorized;

            loginToolStripMenuItem.Visible = showAuthenticationActions && !isAuthorized;
            logoutToolStripMenuItem.Visible = showAuthenticationActions && isAuthorized;
        }

        private void contextMenuStrip1_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateAuthenticationMenuItems();
            _namespaceSelectionMenuController.RefreshMenu();
        }

        private async void continueRegistrationButton_Click(object sender, EventArgs e)
        {
            try
            {
                continueRegistrationButton.Enabled = false;
                await _bootstrapRegistrationService.PrepareRegistrationQrAsync(baseUrlTextBox.Text).ConfigureAwait(true);

                Debug.WriteLine("SHOW QR");
                ShowRegistrationQr();
                _ = StartPollingAfterRenderAsync();
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

            notifyIcon1.Visible = false;
            _activateInstanceCts.Cancel();
            _activateInstanceEvent.Dispose();
            _activateInstanceCts.Dispose();
            _nativePipeMessageServer.Dispose();
            base.OnFormClosing(e);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _isExitRequested = true;
            notifyIcon1.Visible = false;
            Close();
        }

        private void logoutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _authorizationSessionStore.Revoke();
            UpdateAuthenticationMenuItems();
        }

        private async void loginToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                loginToolStripMenuItem.Enabled = false;

                bool authorized = await _authenticationService.EnsureAuthorizedAsync().ConfigureAwait(true);
                if (!authorized)
                {
                    MessageBox.Show(
                        "Login was declined or did not complete.",
                        "Login",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Login", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                loginToolStripMenuItem.Enabled = true;
                UpdateAuthenticationMenuItems();
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAndActivateWindow();
        }
    }
}