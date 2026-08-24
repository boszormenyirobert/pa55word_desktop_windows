namespace TrayApp.TrayMenu.Views
{
    internal sealed class HomeTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            string desktopSessionText = context.HasDesktopSession
                ? "Active desktop session available"
                : "No active desktop session";

            return new TrayMenuViewState
            {
                Eyebrow = "OVERVIEW",
                Title = "Home",
                Description = "The default tray view shows the current active user, active server, and the desktop session state.",
                InsightTitle = "Current state",
                InsightBody = $"Current status is {context.StatusText.ToLowerInvariant()}. Use the left-side menu to switch to Users or Servers without changing the active Home highlight on startup.",
                SelectionLayout = TrayMenuSelectionLayout.HomeSummary,
                SummaryLines = new[]
                {
                    $"Active user: {context.ActiveUserLabel}",
                    $"Active server: {context.ActiveServerLabel}",
                    $"Desktop session: {desktopSessionText}"
                }
            };
        }
    }
}