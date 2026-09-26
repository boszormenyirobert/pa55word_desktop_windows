namespace TrayApp.TrayMenu.Views
{
    internal sealed class AccountsTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            return new TrayMenuViewState
            {
                Eyebrow = "ACCOUNTS",
                Title = "Server accounts",
                Description = "Accounts stored in the local configuration.",
                InsightTitle = string.Empty,
                InsightBody = string.Empty,
                SelectionLayout = TrayMenuSelectionLayout.HomeSummary
            };
        }
    }
}