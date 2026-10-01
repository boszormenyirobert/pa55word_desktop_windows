namespace TrayApp.TrayMenu.Views
{
    internal sealed class DevicesTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            return new TrayMenuViewState
            {
                Eyebrow = "SETTINGS",
                Title = $"Registered devices for the user profile: {context.ActiveUserLabel}",
                Description = "Review the guidance before replacing or deleting a device.",
                InsightTitle = string.Empty,
                InsightBody = string.Empty,
                SelectionLayout = TrayMenuSelectionLayout.HomeSummary
            };
        }
    }
}
