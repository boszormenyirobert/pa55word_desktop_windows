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
using TrayApp.TrayMenu.Controls;

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

        private bool _isDeviceRegistered;
        private RegistrationPromptMode _registrationPromptMode = RegistrationPromptMode.DeviceRegistration;
        private readonly BootstrapRegistrationService _bootstrapRegistrationService = new BootstrapRegistrationService();
        private readonly RegisteredDeviceService _registeredDeviceService = new RegisteredDeviceService();
        private readonly HttpApiClient _httpApiClient = new HttpApiClient();
        private readonly AccountLifecycleService _accountLifecycleService;
        private readonly IAccountDeviceService _accountDeviceService;
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
        private bool _allowInitialFormDisplay;

        public Form1()
        {
            InitializeComponent();
            Opacity = 0;
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            notifyIcon1.ContextMenuStrip = null;

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
            _accountDeviceService = new AccountDeviceService(_httpApiClient, _authenticationService);

            _authStateMonitorService = new AuthStateMonitorService(
                _authenticationService,
                _extensionSessionStateStore,
                HandleAuthorizationStateEvaluated,
                message =>
                {
                    Debug.WriteLine(message);
                    DiagnosticLog.Write(message);
                });

            _extensionSessionStateStore.StateChanged += HandleExtensionSessionStateChanged;
            _authenticationService.DeviceRegistrationRemoved += HandleDeviceRegistrationRemoved;

            var credentialCollectionService = new CredentialCollectionService(
                _registeredDeviceService,
                _httpApiClient,
                _authenticationService,
                _extensionSessionStateStore,
                message => Debug.WriteLine(message));

            _credentialsPresenter = new TrayMenu.Presenters.CredentialsPresenter(
                credentialCollectionService, BuildCredentialCollectionCacheKey);
            _credentialsPresenter.StateChanged += HandleCredentialsStateChanged;

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

            StartPosition = FormStartPosition.CenterScreen;
            MainWindowPresentation.ApplyBranding(this);

            notifyIcon1.Visible = true;
            notifyIcon1.Text = "ZeroIntrusion Agent";

            InitializeQuickPanel();
            UpdateAuthenticationMenuItems();

            _bootstrapRegistrationService.RegistrationCompleted += OnRegistrationCompleted;
            _bootstrapRegistrationService.RegistrationFailed += OnRegistrationFailed;

            // Background UI notifications need a handle before the initial window is shown.
            if (!IsHandleCreated)
            {
                CreateHandle();
            }

            _activateInstanceTask = Task.Run(() => ListenForActivationRequestsAsync(_activateInstanceCts.Token));
            BootstrapRegistration();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                // Keep the native resize frame and system menu, but draw our own caption.
                parameters.Style &= ~0x00C00000;
                return parameters;
            }
        }

        protected override void WndProc(ref Message message)
        {
            const int WmNcHitTest = 0x0084;
            const int HtClient = 1;
            const int HtLeft = 10;
            const int HtRight = 11;
            const int HtTop = 12;
            const int HtTopLeft = 13;
            const int HtTopRight = 14;
            const int HtBottom = 15;
            const int HtBottomLeft = 16;
            const int HtBottomRight = 17;

            base.WndProc(ref message);

            if (message.Msg != WmNcHitTest)
            {
                return;
            }

            message.Result = message.Result.ToInt32() switch
            {
                HtLeft or HtRight => (IntPtr)HtClient,
                HtTopLeft or HtTopRight => (IntPtr)HtTop,
                HtBottomLeft or HtBottomRight => (IntPtr)HtBottom,
                _ => message.Result
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            MainWindowPresentation.ApplyTitleBarTheme(this);
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value && _allowInitialFormDisplay);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_allowInitialFormDisplay)
            {
                BeginInvoke(new Action(ShowAndActivateWindow));
            }
        }

        private async void BootstrapRegistration()
        {
            try
            {
                bool deviceIsRegistered = _bootstrapRegistrationService.LoadOrCreateConfig();
                Debug.WriteLine($"Registration state: {deviceIsRegistered}");

                if (deviceIsRegistered)
                {
                    EnterRegisteredMode();
                    await _accountLifecycleService.RemoveDeletedAccountsAsync();
                    deviceIsRegistered = _registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) &&
                        registrations is { Count: > 0 };

                    if (deviceIsRegistered)
                    {
                        RefreshQuickPanelSelections();
                        UpdateQuickPanelState();
                    }
                }

                if (!deviceIsRegistered)
                {
                    ShowBaseUrlPrompt();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bootstrap registration failed: {ex}");
                MessageBox.Show(ex.Message, "Startup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(OnRegistrationCompleted));
                return;
            }

            if (_showInlineUserRegistration)
            {
                _lastTrayMessage = "User registration is complete.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                ResetInlineUserRegistration();
                _trayMenuController?.SetActiveSection(TrayMenuSection.Login, triggerStateChanged: true);
                RefreshQuickPanelSelections();
                UpdateQuickPanelState();
                UpdateAuthenticationMenuItems();
                return;
            }

            if (_showInlineServerRegistration &&
                _registrationPromptMode is RegistrationPromptMode.ServerRegistration or RegistrationPromptMode.NewServerRegistration)
            {
                _lastTrayMessage = _registrationPromptMode == RegistrationPromptMode.NewServerRegistration
                    ? "Server added successfully."
                    : "Server sync is complete.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                ResetInlineServerRegistrationState(clearInputs: true);

                if (_registrationPromptMode is RegistrationPromptMode.ServerRegistration or RegistrationPromptMode.NewServerRegistration)
                {
                    _trayMenuController?.SetActiveSection(TrayMenuSection.Login, triggerStateChanged: true);
                }

                RefreshQuickPanelSelections();
                UpdateQuickPanelState();
                UpdateAuthenticationMenuItems();
                return;
            }

            EnterRegisteredMode();
        }

        private void OnRegistrationFailed(string message)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(OnRegistrationFailed), message);
                return;
            }

            if (_showInlineUserRegistration)
            {
                _inlineUserRegistrationSubmitting = false;
                _inlineUserRegistrationAwaitingCompletion = false;
                _profileView.RegistrationSubmitButton.Enabled = true;
                _profileView.RegistrationStatusLabel.Text = message;
                UpdateQuickPanelState();
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
                ShowBaseUrlPrompt();
            }

            MessageBox.Show(message, "Registration", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void EnterRegisteredMode()
        {
            _isDeviceRegistered = true;
            EnsureRegisteredServicesStarted();
            _trayMenuController?.SetInitialSection(_authenticationService.IsAuthorizedForActiveNamespace);
            _lastTrayMessage = "Desktop registration is complete.";
            _lastTrayMessageIcon = ToolTipIcon.Info;
            _lastTrayStateUpdatedAt = DateTimeOffset.Now;
            RefreshQuickPanelSelections();
            UpdateQuickPanelState();
            UpdateAuthenticationMenuItems();

            ShowQuickPanel(refreshFromBackend: false);
            HandleTrayMenuSectionChanged();
        }

        private void ShowBaseUrlPrompt()
        {
            bool hasRegisteredUsers = _registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) &&
                registrations != null &&
                registrations.Count > 0;

            _registrationPromptMode = RegistrationPromptMode.DeviceRegistration;
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
                baseUrlTextBox.Text = string.Empty;
            }

            ShowAndActivateWindow();
            baseUrlTextBox.Focus();
        }

        private void ShowServerRegistrationPrompt()
        {
            _registrationPromptMode = RegistrationPromptMode.NewServerRegistration;
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
            device_registration.Text = "Add a server";

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

            Bitmap? qrCode = _bootstrapRegistrationService.QrCode;

            if (qrCode == null)
            {
                MessageBox.Show("QR code is empty");
                return;
            }

            pictureBoxQr.Image = qrCode;
            pictureBoxQr.SizeMode = PictureBoxSizeMode.Zoom;
            restartRegistrationButton.Visible = true;

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

                    BeginInvoke(new Action(() =>
                    {
                        if (_isDeviceRegistered)
                        {
                            ShowQuickPanel(refreshFromBackend: false);
                        }
                        else
                        {
                            ShowBaseUrlPrompt();
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

        private void ShowAndActivateWindow()
        {
            if (IsDisposed)
            {
                return;
            }

            _allowInitialFormDisplay = true;

            MainWindowPresentation.Show(this);
        }

        private void HandleAuthorizationNotificationChanged(string? message, ToolTipIcon icon)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

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

            notifyIcon1.BalloonTipTitle = "Pa55word.com";
            notifyIcon1.BalloonTipText = message;
            notifyIcon1.BalloonTipIcon = icon;
            notifyIcon1.Visible = true;
            notifyIcon1.ShowBalloonTip(5000);
        }

        private void HandleExtensionSessionStateChanged(ExtensionSessionStateResponseDto state)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

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
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(HandleAuthorizationStateEvaluated));
                return;
            }

            _lastTrayStateUpdatedAt = DateTimeOffset.Now;
            UpdateAuthenticationMenuItems();
            UpdateQuickPanelState();
        }

        private void HandleDeviceRegistrationRemoved()
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(HandleDeviceRegistrationRemoved));
                return;
            }

            _credentialsPresenter.Reset();
            _credentialSelectorsFingerprint = string.Empty;
            _lastTrayMessage = AuthenticationService.DeviceRemovedMessage;
            _lastTrayMessageIcon = ToolTipIcon.Warning;
            _lastTrayStateUpdatedAt = DateTimeOffset.Now;

            bool hasRegistrations = _registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) &&
                registrations is { Count: > 0 };
            if (!hasRegistrations)
            {
                _isDeviceRegistered = false;
            }

            _extensionSessionStateStore.MarkInvalid(AuthenticationService.DeviceRemovedReason);

            if (!hasRegistrations)
            {
                CloseQuickPanel();
                UpdateAuthenticationMenuItems();
                ShowBaseUrlPrompt();
                return;
            }

            _trayMenuController?.SetActiveSection(TrayMenuSection.Login);
            RefreshQuickPanelSelections();
            UpdateQuickPanelState();
            UpdateAuthenticationMenuItems();
        }

        private void UpdateAuthenticationMenuItems()
        {
            _sidebar.ApplyActions(new SidebarActionState(_isDeviceRegistered, _authenticationService.IsAuthorized,
                _authenticationInProgress, _quickPanelRefreshInProgress));
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
                _bootstrapRegistrationService.StartRegistrationPolling(pollingBaseUrl);
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
            _activateInstanceEvent.Set();
            _activateInstanceTask?.GetAwaiter().GetResult();
            _activateInstanceEvent.Dispose();
            _activateInstanceCts.Dispose();
            _nativePipeMessageServer.Dispose();
            _authStateMonitorService.Dispose();
            base.OnFormClosing(e);
        }

        private async Task LogoutAsync()
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

        private async Task LoginAsync()
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
                    _authenticationService.RevokeAuthorizationForActiveNamespace();
                    _extensionSessionStateStore.MarkInvalid("authorization_declined");
                    RefreshQuickPanelSelections();
                    _authenticationInProgress = false;
                    UpdateAuthenticationMenuItems();
                    return;
                }

                _extensionSessionStateStore.MarkAuthorized();
                _lastTrayMessage = "Desktop attestation is active.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _trayMenuController?.SetActiveSection(TrayMenuSection.Credentials, triggerStateChanged: true);
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