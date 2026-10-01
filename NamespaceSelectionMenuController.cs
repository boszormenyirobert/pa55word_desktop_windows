using System.Diagnostics;
using System.Windows.Forms;
using TrayApp.Models;
using TrayApp.Services;

namespace TrayApp
{
    internal sealed class NamespaceSelectionMenuController
    {
        private readonly RegisteredDeviceService _registeredDeviceService;
        private readonly AuthorizationSessionStore _authorizationSessionStore;
        private readonly ToolStripMenuItem _namespaceSelectionMenuItem;
        private readonly Action _namespaceSelectionChanged;

        public NamespaceSelectionMenuController(
            RegisteredDeviceService registeredDeviceService,
            AuthorizationSessionStore authorizationSessionStore,
            ToolStripMenuItem namespaceSelectionMenuItem,
            Action namespaceSelectionChanged)
        {
            _registeredDeviceService = registeredDeviceService;
            _authorizationSessionStore = authorizationSessionStore;
            _namespaceSelectionMenuItem = namespaceSelectionMenuItem;
            _namespaceSelectionChanged = namespaceSelectionChanged;
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
                List<NamespaceItemDto> activeNamespaces = registration.Namespaces
                    .Where(namespaceItem => _authorizationSessionStore.HasActiveSessionForNamespace(
                        registration.UserKey,
                        namespaceItem.NamespaceId))
                    .ToList();
                bool hasMultipleActiveNamespaces = activeNamespaces.Count > 1;

                _namespaceSelectionMenuItem.Visible = hasMultipleActiveNamespaces;
                _namespaceSelectionMenuItem.DropDownItems.Clear();

                if (!hasMultipleActiveNamespaces)
                {
                    return;
                }

                string? activeNamespaceId = AppEnvironment.GetActiveNamespaceId();

                foreach (NamespaceItemDto namespaceItem in activeNamespaces)
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

                if (!activeNamespaces.Any(namespaceItem =>
                        string.Equals(namespaceItem.NamespaceId, activeNamespaceId, StringComparison.Ordinal)))
                {
                    AppEnvironment.SetActiveNamespace(activeNamespaces[0].NamespaceId);
                    _namespaceSelectionChanged();
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
            _namespaceSelectionChanged();
            RefreshMenu();
        }

        private void ClearMenu()
        {
            _namespaceSelectionMenuItem.Visible = false;
            _namespaceSelectionMenuItem.DropDownItems.Clear();
        }
    }
}