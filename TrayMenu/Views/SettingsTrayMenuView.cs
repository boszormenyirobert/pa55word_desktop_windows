namespace TrayApp.TrayMenu.Views
{
    internal sealed class SettingsTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            return new TrayMenuViewState
            {
                Eyebrow = "SETTINGS",
                Title = "Settings",
                Description = "Manage your profile, connected servers, registered devices, and account details from one place.",
                InsightTitle = string.Empty,
                InsightBody = string.Empty,
                SelectionLayout = TrayMenuSelectionLayout.None
            };
        }
    }
}