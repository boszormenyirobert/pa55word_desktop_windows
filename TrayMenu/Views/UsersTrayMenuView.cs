namespace TrayApp.TrayMenu.Views
{
    internal sealed class UsersTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            string userList = context.RegisteredUsers.Count > 0
                ? string.Join(", ", context.RegisteredUsers)
                : "No registered users.";

            return new TrayMenuViewState
            {
                Eyebrow = "USERS",
                Title = "Registered users",
                Description = $"Active user: {context.ActiveUserLabel}",
                InsightTitle = "User list",
                InsightBody = $"{userList}\n\nUse the green + Register user action in this view after logging out from the current desktop session.",
                SelectionLayout = TrayMenuSelectionLayout.UserSelector,
                AccountMetricOverride = $"{context.ActiveUserLabel} ({context.RegisteredUserCount} total)"
            };
        }
    }
}