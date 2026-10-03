using TrayApp.TrayMenu.Views;

namespace TrayApp.TrayMenu
{
    internal sealed class TrayMenuViewRegistry
    {
        private readonly Dictionary<TrayMenuSection, ITrayMenuView> _views;

        public TrayMenuViewRegistry()
        {
            _views = new Dictionary<TrayMenuSection, ITrayMenuView>
            {
                [TrayMenuSection.Login] = new LoginTrayMenuView(),
                [TrayMenuSection.Settings] = new SettingsTrayMenuView(),
                [TrayMenuSection.SettingsUsers] = new SettingsUsersTrayMenuView(),
                [TrayMenuSection.SettingsServers] = new SettingsServersTrayMenuView(),
                [TrayMenuSection.Devices] = new DevicesTrayMenuView(),
                [TrayMenuSection.Accounts] = new AccountsTrayMenuView(),
                [TrayMenuSection.Servers] = new SettingsServersTrayMenuView(),
                [TrayMenuSection.Credentials] = new CredentialsTrayMenuView(),
                [TrayMenuSection.Faq] = new FaqTrayMenuView()
            };
        }

        public TrayMenuViewState Build(TrayMenuSection section, TrayMenuContext context)
        {
            return _views.TryGetValue(section, out ITrayMenuView? view)
                ? view.Build(context)
                : _views[TrayMenuSection.Login].Build(context);
        }
    }
}