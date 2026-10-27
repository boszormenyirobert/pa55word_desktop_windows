using TrayApp.TrayMenu.Presenters;
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
        private readonly ManageDatabasesControl _databaseView = new();
        private bool _showInlineServerRegistration;
        private bool _inlineServerRegistrationSubmitting;
        private bool _inlineServerRegistrationAwaitingCompletion;
        private InlineServerRegistrationMode _inlineServerRegistrationMode;
        private string _serverRegistrationStatusMessage = string.Empty;

        private void InitializeDatabaseView()
        {
            _databaseView.ActionChanged += quickPanelSettingsServerActionComboBox_SelectedIndexChanged;
            _databaseView.SubmitRequested += quickPanelServerRegistrationSubmitButton_Click;
            _databaseView.BackRequested += quickPanelServerRegistrationBackButton_Click;
            _databaseView.RestartRequested += quickPanelServerRegistrationRestartButton_Click;
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

        private enum InlineServerRegistrationMode
        {
            NamespaceSync,
            NewServer
        }

        private void quickPanelRegisterNewServerInlineButton_Click(object? sender, EventArgs e)
        {
            ShowInlineServerRegistration(InlineServerRegistrationMode.NewServer);
        }

        private void quickPanelServerRegistrationInlineButton_Click(object? sender, EventArgs e)
        {
            ShowInlineServerRegistration(InlineServerRegistrationMode.NamespaceSync);
        }

        private void ShowInlineServerRegistration(InlineServerRegistrationMode registrationMode)
        {
            if (_inlineServerRegistrationAwaitingCompletion || _inlineServerRegistrationSubmitting)
            {
                return;
            }

            if (_showInlineServerRegistration)
            {
                ResetInlineServerRegistrationState(clearInputs: false);
                UpdateQuickPanelState();
                return;
            }

            _showInlineServerRegistration = true;
            _inlineServerRegistrationMode = registrationMode;
            _serverRegistrationStatusMessage = string.Empty;
            _databaseView.QrPictureBox.Image = null;

            try
            {
                _databaseView.UrlTextBox.Text = AppEnvironment.BackendBaseUrl;
            }
            catch
            {
                _databaseView.UrlTextBox.Text = string.Empty;
            }

            _databaseView.NameTextBox.Text = string.Empty;
            UpdateQuickPanelState();
            _databaseView.UrlTextBox.Focus();
        }

        private void quickPanelServerRegistrationBackButton_Click(object? sender, EventArgs e)
        {
            _bootstrapRegistrationService.CancelRegistrationPolling();
            ResetInlineServerRegistrationState(clearInputs: true);
            _trayMenuController?.SetActiveSection(TrayMenuSection.SettingsServers);
            UpdateQuickPanelState();
        }

        private void quickPanelServerRegistrationRestartButton_Click(object? sender, EventArgs e)
        {
            _bootstrapRegistrationService.CancelRegistrationPolling();
            ResetInlineServerRegistrationState(clearInputs: true);
            UpdateQuickPanelState();
        }

        private async void quickPanelServerRegistrationSubmitButton_Click(object? sender, EventArgs e)
        {
            if (_inlineServerRegistrationSubmitting || _inlineServerRegistrationAwaitingCompletion)
            {
                return;
            }

            try
            {
                bool isNewServerRegistration = _inlineServerRegistrationMode == InlineServerRegistrationMode.NewServer;
                _registrationPromptMode = isNewServerRegistration
                    ? RegistrationPromptMode.NewServerRegistration
                    : RegistrationPromptMode.ServerRegistration;
                _inlineServerRegistrationSubmitting = true;
                _serverRegistrationStatusMessage = isNewServerRegistration
                    ? "Generating QR code to add the server."
                    : "Generating QR code to sync the server.";
                UpdateQuickPanelState();

                if (isNewServerRegistration)
                {
                    await _bootstrapRegistrationService
                        .PrepareNewServerRegistrationQrAsync(
                            _databaseView.UrlTextBox.Text,
                            _databaseView.NameTextBox.Text)
                        .ConfigureAwait(true);
                }
                else
                {
                    await _bootstrapRegistrationService
                        .PrepareServerRegistrationQrAsync(_databaseView.UrlTextBox.Text)
                        .ConfigureAwait(true);
                }

                Bitmap? qrCode = _bootstrapRegistrationService.QrCode;
                if (qrCode == null)
                {
                    throw new InvalidOperationException("QR code is empty.");
                }

                _databaseView.QrPictureBox.Image = qrCode;
                _databaseView.QrPictureBox.Visible = true;
                _databaseView.RestartButton.Visible = true;
                _inlineServerRegistrationAwaitingCompletion = true;
                _serverRegistrationStatusMessage = "QR generated. Scan it in the mobile app to complete registration.";
                _lastTrayMessage = isNewServerRegistration
                    ? "Server QR generated. Scan it in the mobile app to complete registration."
                    : "Server sync QR generated. Scan it in the mobile app to complete registration.";
                _lastTrayMessageIcon = ToolTipIcon.Info;
                _lastTrayStateUpdatedAt = DateTimeOffset.Now;
                _bootstrapRegistrationService.StartRegistrationPolling(_databaseView.UrlTextBox.Text);
            }
            catch (Exception ex)
            {
                _serverRegistrationStatusMessage = ex.Message;
            }
            finally
            {
                _inlineServerRegistrationSubmitting = false;
                UpdateQuickPanelState();
            }
        }
    }
}
