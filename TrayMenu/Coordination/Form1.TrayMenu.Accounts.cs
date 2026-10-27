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
        private readonly AccountsControl _accountsView = new();

        private void UpdateAccountsPresentation()
        {
            bool active = _trayMenuController?.ActiveSection == TrayMenuSection.Accounts;
            _accountsView.Visible = active;
            _quickPanelDetailDescriptionLabel.Size = new Size(574, active ? 60 : 44);
            if (!active)
            {
                return;
            }
            _quickPanelSelectionCard.Visible = false;
            _quickPanelMetricsCard.Visible = false;
            _quickPanelInsightCard.Visible = false;
            _accountsView.LayoutContent(_quickPanelDetailPanel.Height);
            if (_registeredDeviceService.TryReadRegistration(out DesktopRegistration? registration) && registration != null)
            {
                _accountsView.Render(registration.UserKey, registration.Namespaces);
            }
            else
            {
                _accountsView.Render(string.Empty, Array.Empty<NamespaceItemDto>());
            }
        }

        private async Task DeleteAccountFromQuickPanelAsync(string userId, NamespaceItemDto namespaceItem)
        {
            DialogResult confirmation = MessageBox.Show(
                "Removing this server subscription deregisters this device and removes server-side account data for this server. This cannot be undone.",
                "Remove server subscription?",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmation != DialogResult.OK)
            {
                UpdateQuickPanelState();
                return;
            }

            try
            {
                await _accountLifecycleService.DeleteAccountAsync(userId, namespaceItem);

                bool deletedLastAccountForActiveProfile =
                    string.Equals(userId, AppEnvironment.GetActiveUserId(), StringComparison.Ordinal) &&
                    (!_registeredDeviceService.TryReadRegistration(out DesktopRegistration? registration) ||
                     registration == null ||
                     registration.Namespaces.Count == 0);

                if (deletedLastAccountForActiveProfile)
                {
                    _trayMenuController?.SetActiveSection(TrayMenuSection.Login, triggerStateChanged: true);
                }

                RefreshQuickPanelSelections();
                UpdateQuickPanelState();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Server removal failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
