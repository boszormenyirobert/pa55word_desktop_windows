using System.Diagnostics;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;

namespace TrayApp
{
    internal sealed class NamespaceSelectionMenuController
    {
        private readonly RegisteredDeviceService _registeredDeviceService;
        private readonly ToolStripMenuItem _namespaceSelectionMenuItem;

        public NamespaceSelectionMenuController(
            RegisteredDeviceService registeredDeviceService,
            ToolStripMenuItem namespaceSelectionMenuItem)
        {
            _registeredDeviceService = registeredDeviceService;
            _namespaceSelectionMenuItem = namespaceSelectionMenuItem;
        }

        public void RefreshMenu()
        {
            if (!_registeredDeviceService.TryReadRegistration(out DesktopRegistration? registration) || registration == null)
            {
                ClearMenu();
                return;
            }

            try
            {
                bool hasMultipleNamespaces = registration.Namespaces.Count > 1;

                _namespaceSelectionMenuItem.Visible = hasMultipleNamespaces;
                _namespaceSelectionMenuItem.DropDownItems.Clear();

                if (!hasMultipleNamespaces)
                {
                    return;
                }

                string? activeNamespaceId = AppEnvironment.GetActiveNamespaceId();

                foreach (NamespaceItemDto namespaceItem in registration.Namespaces)
                {
                    var menuItem = new ToolStripMenuItem(namespaceItem.Namespace)
                    {
                        Tag = namespaceItem.NamespaceId,
                        Checked = string.Equals(namespaceItem.NamespaceId, activeNamespaceId, StringComparison.Ordinal),
                        CheckOnClick = true
                    };

                    menuItem.Click += NamespaceSelectionMenuItem_Click;
                    _namespaceSelectionMenuItem.DropDownItems.Add(menuItem);
                }

                if (string.IsNullOrWhiteSpace(activeNamespaceId) && registration.Namespaces.Count > 0)
                {
                    AppEnvironment.SetActiveNamespace(registration.Namespaces[0].NamespaceId);
                    RefreshMenu();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to refresh namespace selection menu: {ex.Message}");
                ClearMenu();
            }
        }

        private void NamespaceSelectionMenuItem_Click(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem menuItem || menuItem.Tag is not string namespaceId)
            {
                return;
            }

            AppEnvironment.SetActiveNamespace(namespaceId);
            RefreshMenu();
        }

        private void ClearMenu()
        {
            _namespaceSelectionMenuItem.Visible = false;
            _namespaceSelectionMenuItem.DropDownItems.Clear();
        }
    }
}