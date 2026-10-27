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
        private readonly SettingsControl _settingsView = new();
        private string _settingsAccessMessage = string.Empty;

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

        private void LayoutSettingsOverview()
        {
            _settingsView.LayoutContent(_quickPanelDetailPanel.Height,
                _trayMenuController?.ActiveSection is TrayMenuSection.Settings or TrayMenuSection.Recovery);
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
    }
}
