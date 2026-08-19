using System.Diagnostics;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;

namespace TrayApp
{
    internal sealed class UserSelectionMenuController
    {
        private readonly RegisteredDeviceService _registeredDeviceService;
        private readonly ToolStripMenuItem _userSelectionMenuItem;
        private readonly Action _refreshNamespaceMenu;
        private readonly Func<bool> _isAuthorized;

        public UserSelectionMenuController(
            RegisteredDeviceService registeredDeviceService,
            ToolStripMenuItem userSelectionMenuItem,
            Action refreshNamespaceMenu,
            Func<bool> isAuthorized)
        {
            _registeredDeviceService = registeredDeviceService;
            _userSelectionMenuItem = userSelectionMenuItem;
            _refreshNamespaceMenu = refreshNamespaceMenu;
            _isAuthorized = isAuthorized;
        }

        public void RefreshMenu()
        {
            if (_isAuthorized())
            {
                ClearMenu();
                return;
            }

            if (!_registeredDeviceService.TryReadRegistrations(out IReadOnlyList<DesktopRegistration>? registrations) ||
                registrations == null ||
                registrations.Count == 0)
            {
                ClearMenu();
                return;
            }

            try
            {
                _userSelectionMenuItem.Visible = true;
                _userSelectionMenuItem.DropDownItems.Clear();

                string? activeUserId = AppEnvironment.GetActiveUserId();

                foreach (DesktopRegistration registration in registrations)
                {
                    var menuItem = new ToolStripMenuItem(registration.DisplayUserName)
                    {
                        Tag = registration.UserKey,
                        Checked = string.Equals(registration.UserKey, activeUserId, StringComparison.Ordinal),
                        CheckOnClick = true
                    };

                    menuItem.Click += UserSelectionMenuItem_Click;
                    _userSelectionMenuItem.DropDownItems.Add(menuItem);
                }

                if (string.IsNullOrWhiteSpace(activeUserId))
                {
                    AppEnvironment.SetActiveUser(registrations[0].UserKey);
                    RefreshMenu();
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to refresh user selection menu: {ex.Message}");
                ClearMenu();
            }
        }

        private void UserSelectionMenuItem_Click(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem menuItem || menuItem.Tag is not string userId)
            {
                return;
            }

            AppEnvironment.SetActiveUser(userId);
            RefreshMenu();
            _refreshNamespaceMenu();
        }

        private void ClearMenu()
        {
            _userSelectionMenuItem.Visible = false;
            _userSelectionMenuItem.DropDownItems.Clear();
        }
    }
}