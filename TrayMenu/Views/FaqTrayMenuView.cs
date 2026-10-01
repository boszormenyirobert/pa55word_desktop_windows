namespace TrayApp.TrayMenu.Views
{
    internal sealed class FaqTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            return new TrayMenuViewState
            {
                Eyebrow = "SUPPORT",
                Title = "Help and FAQ",
                Description = "Registration, servers, profiles, credentials, and device access.",
                InsightTitle = string.Empty,
                InsightBody = string.Empty,
                SelectionLayout = TrayMenuSelectionLayout.HomeSummary
            };
        }
    }
}
