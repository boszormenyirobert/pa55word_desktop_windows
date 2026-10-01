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
                Description = "Deleting an account completely deregisters all registered devices, credentials, and recovery data, including email addresses and phone numbers. All user-related information is permanently deleted and cannot be recovered.",
                InsightTitle = string.Empty,
                InsightBody = string.Empty,
                SelectionLayout = TrayMenuSelectionLayout.HomeSummary
            };
        }
    }
}