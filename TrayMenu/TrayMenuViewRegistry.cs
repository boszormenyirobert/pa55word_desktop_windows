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
                [TrayMenuSection.Overview] = new HomeTrayMenuView(),
                [TrayMenuSection.Users] = new UsersTrayMenuView(),
                [TrayMenuSection.Servers] = new ServersTrayMenuView(),
                [TrayMenuSection.Credentials] = new CredentialsTrayMenuView()
            };
        }

        public TrayMenuViewState Build(TrayMenuSection section, TrayMenuContext context)
        {
            return _views.TryGetValue(section, out ITrayMenuView? view)
                ? view.Build(context)
                : _views[TrayMenuSection.Overview].Build(context);
        }
    }
}