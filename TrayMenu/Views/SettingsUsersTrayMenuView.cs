namespace TrayApp.TrayMenu.Views
{
    internal sealed class SettingsUsersTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            return new TrayMenuViewState
            {
                Eyebrow = "SETTINGS",
                Title = "Profile",
                Description = "Choose a profile action.",
                InsightTitle = string.Empty,
                InsightBody = string.Empty,
                SelectionLayout = TrayMenuSelectionLayout.SettingsUserActions
            };
        }
    }
}