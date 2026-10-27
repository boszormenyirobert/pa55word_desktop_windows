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
        private void HandleTrayMenuSectionChanged()
        {
            TrayMenuSection? activeSection = _trayMenuController?.ActiveSection;

            if (activeSection != TrayMenuSection.Credentials)
            {
                RefreshQuickPanelSelections();
            }

            UpdateQuickPanelState();

            if (activeSection == TrayMenuSection.Credentials)
            {
                if (_authenticationService.IsAuthorizedForActiveNamespace)
                {
                    _ = LoadCredentialCollectionAsync(forceRefresh: true);
                }

                return;
            }

            if (activeSection == TrayMenuSection.Devices && !_devicesLoadedOnce)
            {
                _ = LoadAccountDevicesAsync();
            }
        }

        private void ApplyQuickPanelSectionAvailability()
        {
            bool hasAuthenticatedUser = _authenticationService.IsAuthorized;
            bool credentialsSectionEnabled = _authenticationService.IsAuthorizedForActiveNamespace;

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

            bool settingsSectionActive = (_trayMenuController?.ActiveSection is
                TrayMenuSection.Settings or
                TrayMenuSection.SettingsUsers or
                TrayMenuSection.SettingsServers or
                TrayMenuSection.Devices or
                TrayMenuSection.Accounts) ||
                _trayMenuController?.ActiveSection == TrayMenuSection.Servers && _showInlineServerRegistration;
            _sidebar.ApplyNavigation(new SidebarNavigationState(_isDeviceRegistered, credentialsSectionEnabled,
                _trayMenuController?.ActiveSection, settingsSectionActive));
        }

        private void notifyIcon1_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenCredentialsFromTray();
            }
        }

        private void OpenCredentialsFromTray()
        {
            if (_isDeviceRegistered)
            {
                _trayMenuController?.SetInitialSection(_authenticationService.IsAuthorizedForActiveNamespace);
                ShowQuickPanel(refreshFromBackend: false);
                HandleTrayMenuSectionChanged();
                return;
            }

            ShowBaseUrlPrompt();
        }
    }
}
