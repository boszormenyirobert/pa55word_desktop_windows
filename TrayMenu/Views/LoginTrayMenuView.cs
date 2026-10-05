namespace TrayApp.TrayMenu.Views
{
    internal sealed class LoginTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            return new TrayMenuViewState
            {
                Eyebrow = "LOGIN",
                Title = "Select account and server",
                Description = string.IsNullOrWhiteSpace(context.LoginErrorMessage)
                    ? "Choose the account and registered server you want to use."
                    : context.LoginErrorMessage,
                InsightTitle = string.Empty,
                InsightBody = string.Empty,
                SelectionLayout = TrayMenuSelectionLayout.LoginSelectors,
                ShowUserSelector = context.RegisteredUserCount > 1
            };
        }
    }
}