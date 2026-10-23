using System.Drawing;
using System.Net;
using System.Security.Cryptography;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu.Controls;

namespace TrayApp.TrayMenu
{
    internal sealed class RecoveryControl : UserControl
    {
        private readonly DesktopRegistration _registration;
        private readonly Func<RecoveryTarget, DesktopRecoveryService> _createService;
        private readonly TrayDropdown _servers = new() { Name = "RecoveryServer" };
        private readonly Label _serverAddress = new() { AutoSize = true, MaximumSize = new Size(600, 0) };
        private readonly Label _status = new() { Name = "RecoveryStatus", AutoSize = true, MaximumSize = new Size(600, 0) };
        private readonly Label _expiry = new() { AutoSize = true };
        private readonly Label _error = new() { Name = "RecoveryError", AutoSize = true, MaximumSize = new Size(600, 0), ForeColor = Color.FromArgb(255, 166, 166) };
        private readonly TextBox _emailCode = CodeInput("RecoveryEmailCode");
        private readonly TextBox _pairingCode = CodeInput("RecoveryPairingCode");
        private readonly Button _primary = ActionButton("Start recovery", "RecoveryPrimary");
        private readonly Button _confirm = ActionButton("Confirm email code", "RecoveryConfirm");
        private readonly Button _resend = ActionButton("Send a new email code", "RecoveryResend");
        private readonly Button _pair = ActionButton("Select this mobile", "RecoveryPair");
        private readonly Button _refresh = ActionButton("Refresh status", "RecoveryRefresh");
        private readonly Button _cancel = ActionButton("Cancel recovery", "RecoveryCancel");
        private readonly FlowLayoutPanel _cancelConfirmation = new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Visible = false };
        private readonly PictureBox _qr = new() { Name = "RecoveryQr", Size = new Size(230, 230), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
        private readonly FlowLayoutPanel _emailPanel = new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        private readonly FlowLayoutPanel _pairPanel = new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        private readonly Label _selection = new() { Name = "RecoverySelection", AutoSize = true, MaximumSize = new Size(590, 0) };
        private readonly System.Windows.Forms.Timer _poll = new() { Interval = 5000 };
        private readonly CancellationTokenSource _lifetime = new();
        private DesktopRecoveryService? _service;
        private bool _busy;
        private bool _synchronized;
        private bool _pollPaused;
        private bool _resourcesDisposed;
        private string? _qrId;
        private string? _renderState;
        public event EventHandler? BackRequested;

        public RecoveryControl(DesktopRegistration registration, Func<RecoveryTarget, DesktopRecoveryService>? createService = null)
        {
            _registration = registration;
            _createService = createService ?? (target => new DesktopRecoveryService(target));
            BackColor = Color.FromArgb(31, 33, 57);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5F);
            Size = new Size(578, 352);
            AutoScaleMode = AutoScaleMode.Dpi;
            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(12)
            };
            Controls.Add(layout);
            layout.Controls.Add(new Label
            {
                Text = "Recover access on a new mobile phone",
                AutoSize = true,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12)
            });
            layout.Controls.Add(Description($"Profile: {registration.DisplayUserName}"));
            layout.Controls.Add(Description("Choose the registered server. Enter the email code on this desktop, scan the QR on the new phone, then enter the phone's separate pairing code here."));
            layout.Controls.Add(Description("Database / server"));
            _servers.Width = 590;
            layout.Controls.Add(_servers);
            layout.Controls.Add(_serverAddress);
            layout.Controls.Add(_status);
            layout.Controls.Add(_expiry);
            layout.Controls.Add(_error);
            layout.Controls.Add(_primary);
            _emailPanel.Controls.Add(Description("Email confirmation code (12 digits, including leading zeroes)"));
            _emailPanel.Controls.Add(_emailCode);
            var emailActions = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
            emailActions.Controls.Add(_confirm); emailActions.Controls.Add(_resend);
            _emailPanel.Controls.Add(emailActions);
            layout.Controls.Add(_emailPanel);
            _pairPanel.Controls.Add(Description("On the new phone, open Recovery proces and scan this QR. Confirm the server address on the phone."));
            _pairPanel.Controls.Add(_qr);
            _pairPanel.Controls.Add(Description("Enter the separate 12-digit pairing code displayed on the new phone:"));
            _pairPanel.Controls.Add(_pairingCode);
            _pairPanel.Controls.Add(_pair);
            layout.Controls.Add(_pairPanel);
            layout.Controls.Add(_selection);
            var footer = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
            footer.Controls.Add(_refresh); footer.Controls.Add(_cancel);
            var close = ActionButton("Back to profiles", "RecoveryBack");
            close.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
            footer.Controls.Add(close);
            layout.Controls.Add(footer);
            _cancelConfirmation.Controls.Add(Description("Cancel this recovery? This does not delete any existing device."));
            var cancelActions = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
            var cancelYes = ActionButton("Yes, cancel recovery", "RecoveryCancelConfirm");
            var cancelNo = ActionButton("Keep recovery", "RecoveryCancelKeep");
            cancelActions.Controls.Add(cancelYes); cancelActions.Controls.Add(cancelNo);
            _cancelConfirmation.Controls.Add(cancelActions);
            layout.Controls.Add(_cancelConfirmation);
            layout.Controls.Add(Description("Leaving this card does not cancel recovery. Return to Settings > Recovery with the same profile and server to resume. Email and pairing codes are different. Recovery expires after 30 minutes."));
            layout.SizeChanged += (_, _) => ResizeContent(layout);
            ResizeContent(layout);
            _primary.Click += async (_, _) => await RunAsync(PrimaryAsync);
            _confirm.Click += async (_, _) => await RunAsync(async () =>
            {
                await _service!.ConfirmAsync(_emailCode.Text, _lifetime.Token);
                _emailCode.Clear();
                await SynchronizeAsync();
            });
            _resend.Click += async (_, _) => await RunAsync(async () =>
            {
                await _service!.ResendAsync(_lifetime.Token); _emailCode.Clear(); await SynchronizeAsync();
            });
            _pair.Click += async (_, _) => await RunAsync(async () =>
            {
                await _service!.PairAsync(_pairingCode.Text, _lifetime.Token);
                _pairingCode.Clear(); await SynchronizeAsync();
            });
            _refresh.Click += async (_, _) => await RunAsync(SynchronizeAsync);
            _cancel.Click += (_, _) =>
            {
                _cancelConfirmation.Visible = true;
                layout.ScrollControlIntoView(_cancelConfirmation);
            };
            cancelNo.Click += (_, _) => _cancelConfirmation.Visible = false;
            cancelYes.Click += async (_, _) => await RunAsync(async () =>
            {
                await _service!.CancelAsync(_lifetime.Token);
                _synchronized = true; ClearCodes();
            });
            _servers.SelectionChangeCommitted += (_, _) => SelectServer();
            foreach (var server in registration.Namespaces)
                _servers.Items.Add(new ServerOption(server));
            if (_servers.Items.Count > 0)
            {
                _servers.SetSelectedIndexSilently(0);
                SelectServer();
            }
            else { _error.Text = "This profile has no registered database. Recovery cannot start."; Render(); }
            _poll.Tick += async (_, _) =>
            {
                if (!Visible || _resourcesDisposed) return;
                if (_service != null && !_busy && _synchronized && !_pollPaused &&
                    !_service.IsTerminal && !_service.HasPendingRequest && !string.IsNullOrEmpty(_service.Session.RecoveryId))
                    await RunAsync(SynchronizeAsync, polling: true);
            };
            VisibleChanged += (_, _) =>
            {
                if (_resourcesDisposed) return;
                _poll.Enabled = Visible;
                if (!Visible)
                {
                    ClearCodes(); ClearQr();
                    if (_service != null && !_service.IsTerminal &&
                        !string.IsNullOrEmpty(_service.Session.RecoveryId)) _synchronized = false;
                }
                else Render();
            };
            _poll.Enabled = Visible;
        }

        private void ResizeContent(FlowLayoutPanel layout)
        {
            int width = Math.Max(240, layout.ClientSize.Width - layout.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 8);
            _servers.Width = width;
            void ResizeChildren(Control parent)
            {
                foreach (Control child in parent.Controls)
                {
                    if (child is Label or FlowLayoutPanel) child.MaximumSize = new Size(width, 0);
                    if (child is FlowLayoutPanel) ResizeChildren(child);
                }
            }
            ResizeChildren(layout);
        }

        private void SelectServer()
        {
            _service?.Dispose(); _service = null;
            _synchronized = false; _pollPaused = false;
            _cancelConfirmation.Visible = false;
            _error.Text = string.Empty; _serverAddress.Text = string.Empty;
            ClearCodes(); ClearQr();
            try
            {
                if (_servers.SelectedItem is not ServerOption server) return;
                var target = RecoveryTarget.From(_registration, server.Value);
                _serverAddress.Text = target.BaseUrl;
                _service = _createService(target);
                _synchronized = _service.IsTerminal || (_service.Session.Status == "not_started" && !_service.HasPendingRequest);
            }
            catch (Exception error) when (error is InvalidDataException or System.Text.Json.JsonException or CryptographicException or IOException or UnauthorizedAccessException)
            {
                _error.Text = "Cannot open recovery: " + SafeLocalError(error);
            }
            Render();
        }

        private async Task PrimaryAsync()
        {
            if (_service == null) return;
            if (_service.HasPendingRequest)
            {
                await _service.RetryAsync(_lifetime.Token);
                _emailCode.Clear(); _pairingCode.Clear();
                if (!_service.IsTerminal) await SynchronizeAsync();
            }
            else if (_service.IsTerminal)
            {
                _service.Reset(); _synchronized = true; _error.Text = string.Empty; ClearCodes();
            }
            else if (!_synchronized) await SynchronizeAsync();
            else if (_service.Session.Status == "not_started")
            {
                await _service.StartAsync(_lifetime.Token);
                await SynchronizeAsync();
            }
            else if (_service.Session.Status == "mobile_selected")
            {
                await _service.UploadDekAsync(_lifetime.Token);
                await SynchronizeAsync();
            }
        }

        private async Task SynchronizeAsync()
        {
            await _service!.RefreshAsync(_lifetime.Token);
            _synchronized = true;
            _pollPaused = false;
        }

        private async Task RunAsync(Func<Task> action, bool polling = false)
        {
            if (_busy || _resourcesDisposed || _service == null) return;
            _busy = true;
            if (!polling) _cancelConfirmation.Visible = false;
            if (!polling) _error.Text = string.Empty;
            if (!polling) Render();
            try { await action(); if (!_resourcesDisposed) _error.Text = string.Empty; }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (RecoveryClientException error)
            {
                if (_resourcesDisposed) return;
                _error.Text = error.Message + (error.RetryAt is { } retry ? $" Retry after {retry.LocalDateTime:HH:mm:ss}." : string.Empty);
                if (error.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Gone ||
                    _service.IsTerminal) { _synchronized = true; ClearCodes(); }
                _pollPaused = true;
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
            {
                if (_resourcesDisposed) return;
                _error.Text = "The request could not be confirmed. Check the connection, then retry. A pending request keeps the same operation ID; do not start another recovery.";
                _pollPaused = true;
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or IOException or
                CryptographicException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                if (_resourcesDisposed) return;
                _error.Text = SafeLocalError(error);
                _pollPaused = true;
            }
            finally
            {
                _busy = false;
                if (_resourcesDisposed) ReleaseSessionResources();
                else
                {
                    if (!Visible && _service != null && !_service.IsTerminal &&
                        !string.IsNullOrEmpty(_service.Session.RecoveryId)) _synchronized = false;
                    Render(force: !polling);
                }
            }
        }

        private static string SafeLocalError(Exception error) => error switch
        {
            ArgumentException => error.Message,
            InvalidDataException => error.Message,
            InvalidOperationException => error.Message,
            CryptographicException => "The TPM key, mobile public key or protected recovery state could not be used. No new desktop key was created.",
            _ => "Recovery state could not be read or saved. Check local file permissions and available storage before retrying."
        };

        private void Render(bool force = true)
        {
            if (_resourcesDisposed) return;
            var session = _service?.Session;
            bool available = _service != null && !_busy;
            bool pending = _service?.HasPendingRequest == true;
            bool terminal = _service?.IsTerminal == true;
            bool ready = available && _synchronized && !pending && !terminal;
            bool throttled = _service?.RetryAt > DateTimeOffset.UtcNow;
            bool hasActiveRecovery = !string.IsNullOrEmpty(session?.RecoveryId) && session.Status != "not_started";
            bool deadlinePassed = hasActiveRecovery && !terminal && session?.ExpiresAt <= DateTimeOffset.UtcNow;
            bool resendCoolingDown = session?.EmailResendAt > DateTimeOffset.UtcNow;
            string renderState = string.Join("|", Visible, _busy, _synchronized, _pollPaused, _error.Text,
                session?.RecoveryId, session?.Status, session?.ExpiresAt, session?.EmailResendAt, _service?.RetryAt,
                _service?.HasPendingRequest, _service?.IsTerminal, session?.Selected?.MobileDeviceId,
                session?.Selected?.DeviceName, session?.Selected?.PublicKey, throttled, deadlinePassed, resendCoolingDown);
            if (!force && renderState == _renderState) return;
            _servers.Enabled = !_busy;
            _status.Text = _busy ? "Contacting the recovery server..." : StatusText(session?.Status, _synchronized, pending);
            if (!_busy && deadlinePassed)
                _status.Text = "The recovery deadline has passed. Refresh the server status before starting again.";
            _expiry.Text = hasActiveRecovery && session?.ExpiresAt is { } expires
                ? $"Recovery deadline: {expires.LocalDateTime:yyyy-MM-dd HH:mm:ss}" : string.Empty;
            _primary.Text = pending ? "Retry last request" : terminal ? "New recovery" : !_synchronized ? "Resume recovery"
                : session?.Status == "mobile_selected" ? "Transfer DEK to selected mobile" : "Start recovery";
            _primary.Visible = pending || terminal || !_synchronized || session?.Status is "not_started" or "mobile_selected";
            _primary.Enabled = available && !throttled && (!deadlinePassed || pending || !_synchronized);
            _emailPanel.Visible = _synchronized && !terminal && session?.Status == "email_pending" && !pending;
            _confirm.Enabled = ready && !throttled && !deadlinePassed;
            _resend.Enabled = ready && !throttled && !deadlinePassed && !resendCoolingDown;
            _resend.Text = resendCoolingDown ? "New email code (wait 60 seconds)" : "Send a new email code";
            _emailCode.Enabled = ready;
            bool showQr = Visible && _synchronized && session?.Status == "email_confirmed" && !pending &&
                session.ExpiresAt > DateTimeOffset.UtcNow;
            _pairPanel.Visible = showQr;
            _pair.Enabled = ready && !throttled && !deadlinePassed;
            _pairingCode.Enabled = ready;
            if (showQr && _qrId != session!.RecoveryId)
            {
                var generator = new QrService(); generator.GenerateQrCode(_service!.QrPayload());
                var image = generator.GetQrCode() ?? throw new InvalidOperationException("Unable to generate recovery QR.");
                ClearQr(); _qr.Image = image; _qrId = session.RecoveryId;
            }
            else if (!showQr) ClearQr();
            _selection.Text = session?.Selected is { } selected && !terminal
                ? $"Selected mobile: {selected.DeviceName}\nDevice ID: {selected.MobileDeviceId}\nKey SHA-256: {Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(selected.PublicKey)))}"
                : string.Empty;
            _refresh.Enabled = available && !throttled && !string.IsNullOrEmpty(session?.RecoveryId) && !terminal;
            _cancel.Enabled = ready && !throttled && !string.IsNullOrEmpty(session?.RecoveryId);
            if (!_cancel.Enabled) _cancelConfirmation.Visible = false;
            _renderState = renderState;
        }

        internal static string StatusText(string? status, bool synchronized, bool pending)
        {
            if (pending) return "A previous request has an unconfirmed outcome. Retry it before continuing.";
            if (!synchronized && status != null) return "Saved recovery found. Resume to verify its current server state.";
            return status switch
            {
                "not_started" => "Ready. Start recovery to send an email confirmation code.",
                "delivery_pending" => "Email delivery is pending. Wait, then refresh or retry the original request.",
                "email_pending" => "Check the account's registered email address. Enter its code on this desktop.",
                "email_confirmed" => "Email confirmed. Pair the new phone using the QR and the phone's separate code.",
                "mobile_selected" => "Mobile selected. Transfer the existing DEK encrypted for this mobile only.",
                "dek_ready" => "Encrypted DEK transferred. Waiting for the mobile to save it and confirm activation.",
                "completed" => "Recovery completed. The new mobile is activated.",
                "cancelled" => "Recovery cancelled. No additional mobile will be activated by this recovery.",
                "locked" => "Recovery locked after too many incorrect codes. This recovery cannot continue.",
                "delivery_failed" => "Email delivery failed or is uncertain. Its code is invalid; no automatic resend was made.",
                "expired" => "Recovery expired or is no longer active.",
                "revoked" => "Recovery authorization was revoked or the registration changed.",
                _ => "Select a registered HTTP or HTTPS database to recover."
            };
        }

        private static Label Description(string text) => new()
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(590, 0),
            Margin = new Padding(0, 6, 0, 8)
        };
        private static TextBox CodeInput(string name) => new()
        {
            Name = name,
            Width = 220,
            MaxLength = 12,
            Font = new Font("Consolas", 14),
            AccessibleName = name,
            UseSystemPasswordChar = true
        };
        private static Button ActionButton(string text, string name) => new()
        {
            Text = text,
            Name = name,
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(46, 48, 77),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        private void ClearCodes() { _emailCode.Clear(); _pairingCode.Clear(); }
        private void ClearQr() { var image = _qr.Image; _qr.Image = null; image?.Dispose(); _qrId = null; }

        private void ReleaseSessionResources() { _service?.Dispose(); _service = null; _lifetime.Dispose(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_resourcesDisposed)
            {
                _resourcesDisposed = true;
                _poll.Dispose(); _lifetime.Cancel();
                if (!_busy) ReleaseSessionResources();
                ClearQr();
            }
            base.Dispose(disposing);
        }
        private sealed record ServerOption(NamespaceItemDto Value)
        {
            public override string ToString() => $"{Value.Namespace} - {Value.Url}";
        }
    }
}
