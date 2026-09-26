namespace TrayApp.TrayMenu.Views
{
    internal sealed class SettingsServersTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            return new TrayMenuViewState
            {
                Eyebrow = "SETTINGS",
                Title = "Servers",
                Description = "Choose a server action.",
                InsightTitle = string.Empty,
                InsightBody = string.Empty,
                SelectionLayout = TrayMenuSelectionLayout.SettingsServerActions
            };
        }
    }
}