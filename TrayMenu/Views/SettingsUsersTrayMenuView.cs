namespace TrayApp.TrayMenu.Views
{
    internal sealed class SettingsUsersTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            return new TrayMenuViewState
            {
                Eyebrow = "SETTINGS",
                Title = "User accounts",
                Description = "Choose an account action.",
                InsightTitle = string.Empty,
                InsightBody = string.Empty,
                SelectionLayout = TrayMenuSelectionLayout.SettingsUserActions
            };
        }
    }
}