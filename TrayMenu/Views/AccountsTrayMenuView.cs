namespace TrayApp.TrayMenu.Views
{
    internal sealed class AccountsTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            return new TrayMenuViewState
            {
                Eyebrow = "ACCOUNTS",
                Title = "User account",
                Description = "Deleting an account is an irreversible process.",
                InsightTitle = string.Empty,
                InsightBody = string.Empty,
                SelectionLayout = TrayMenuSelectionLayout.HomeSummary
            };
        }
    }
}