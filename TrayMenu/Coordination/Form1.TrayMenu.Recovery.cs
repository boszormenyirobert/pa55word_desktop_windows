using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;
using TrayApp.TrayMenu;

namespace TrayApp
{
    public partial class Form1
    {
        private RecoveryControl? _recoveryControl;
        private bool _updatingRecoveryProfiles;
        private string _selectedRecoveryProfileUserId = string.Empty;

        private void InitializeRecoveryPanel()
        {
            _settingsView.RecoveryProfileChanged += RecoveryProfileComboBox_SelectedIndexChanged;
            _settingsView.RecoveryOpenRequested += OpenRecovery;
        }

        private void PopulateRecoveryProfiles()
        {
            string previouslySelectedUserId = _selectedRecoveryProfileUserId;
            _updatingRecoveryProfiles = true;

            try
            {
                _settingsView.RecoveryProfileComboBox.Items.Clear();
                _settingsView.RecoveryProfileComboBox.Items.Add(new SelectionOption("Select profile", string.Empty));
                _settingsView.RecoveryStatusLabel.Text = string.Empty;
                _settingsView.RecoveryStatusLabel.Visible = false;

                if (!_registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) ||
                    registrations == null)
                {
                    _settingsView.RecoveryStatusLabel.Text = "Unable to load registered profiles on this desktop.";
                    _settingsView.RecoveryStatusLabel.Visible = true;
                }
                else
                {
                    foreach (DesktopRegistration registration in registrations)
                    {
                        _settingsView.RecoveryProfileComboBox.Items.Add(
                            new SelectionOption(registration.DisplayUserName, registration.UserKey));
                    }

                    if (registrations.Count == 0)
                    {
                        _settingsView.RecoveryStatusLabel.Text = "No profiles are registered on this desktop.";
                        _settingsView.RecoveryStatusLabel.Visible = true;
                    }
                }

                _settingsView.RecoveryProfileComboBox.Enabled = _settingsView.RecoveryProfileComboBox.Items.Count > 1;
                int selectedIndex = 0;
                for (int index = 1; index < _settingsView.RecoveryProfileComboBox.Items.Count; index++)
                {
                    if (_settingsView.RecoveryProfileComboBox.Items[index] is SelectionOption option &&
                        string.Equals(option.Value, previouslySelectedUserId, StringComparison.Ordinal))
                    {
                        selectedIndex = index;
                        break;
                    }
                }

                _settingsView.RecoveryProfileComboBox.SetSelectedIndexSilently(selectedIndex);
                _selectedRecoveryProfileUserId =
                    _settingsView.RecoveryProfileComboBox.Items[selectedIndex] is SelectionOption selectedOption
                        ? selectedOption.Value
                        : string.Empty;
                UpdateRecoveryDescription();
            }
            finally
            {
                _updatingRecoveryProfiles = false;
            }
        }

        private void RecoveryProfileComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_updatingRecoveryProfiles)
            {
                return;
            }

            _selectedRecoveryProfileUserId =
                _settingsView.RecoveryProfileComboBox.SelectedItem is SelectionOption option
                    ? option.Value
                    : string.Empty;
            UpdateRecoveryDescription();
        }

        private void UpdateRecoveryDescription()
        {
            CloseRecoveryCard();
            bool profileSelected = !string.IsNullOrWhiteSpace(_selectedRecoveryProfileUserId);
            _settingsView.RecoveryOpenButton.Visible = profileSelected;
            _settingsView.RecoveryDescriptionBox.Visible = profileSelected;
            _settingsView.RecoveryStatusLabel.Visible =
                !profileSelected && !string.IsNullOrWhiteSpace(_settingsView.RecoveryStatusLabel.Text);
            _settingsView.RecoveryDescriptionBox.Text = profileSelected
                ? "Use recovery when your registered mobile phone has been damaged or lost. Open recovery, choose the registered server, then enter the 12-digit code sent to the account's email address. On the new mobile phone, choose Recovery proces and scan the QR. Enter the separate code displayed on that phone here, then transfer the encrypted key. Recovery is complete only after the phone confirms activation. The existing desktop key and encrypted DEK must still be available."
                : string.Empty;
        }

        private void OpenRecovery(object? sender, EventArgs e)
        {
            if (_authenticationService.IsAuthorized || string.IsNullOrWhiteSpace(_selectedRecoveryProfileUserId))
                return;
            try
            {
                var registration = new RegistrationConfigStore().ReadRegistration(_selectedRecoveryProfileUserId);
                if (!string.Equals(registration.UserKey, _selectedRecoveryProfileUserId, StringComparison.Ordinal))
                    throw new InvalidDataException("The selected profile is no longer registered.");
                ShowRecoveryCard(new RecoveryControl(registration));
            }
            catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                _settingsView.RecoveryStatusLabel.Text = "The selected profile's recovery configuration could not be loaded. Check its database registration and local file permissions.";
                _settingsView.RecoveryStatusLabel.Visible = true;
                _settingsView.RecoveryOpenButton.Visible = false;
            }
        }

        private void ShowRecoveryCard(RecoveryControl recovery)
        {
            CloseRecoveryCard();
            _recoveryControl = recovery;
            recovery.Dock = DockStyle.Fill;
            _settingsView.RecoveryProfileComboBox.Enabled = false;
            _settingsView.RecoveryOpenButton.Enabled = false;
            recovery.BackRequested += (_, _) => CloseRecoveryCard();
            _settingsView.RecoveryPanel.Controls.Add(recovery);
            recovery.BringToFront();
        }

        private void CloseRecoveryCard()
        {
            if (_recoveryControl == null) return;
            _settingsView.RecoveryPanel.Controls.Remove(_recoveryControl);
            _recoveryControl.Dispose();
            _recoveryControl = null;
            _settingsView.RecoveryProfileComboBox.Enabled = true;
            _settingsView.RecoveryOpenButton.Enabled = true;
        }
    }
}