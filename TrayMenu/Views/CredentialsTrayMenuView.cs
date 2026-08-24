namespace TrayApp.TrayMenu.Views
{
    internal sealed class CredentialsTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            string description = context.CredentialsLoading
                ? "Loading credential groups and entries for the active server."
                : "Browse credential groups and filter the available entries for the active server.";

            string insightBody;

            if (!string.IsNullOrWhiteSpace(context.CredentialsErrorMessage))
            {
                insightBody = context.CredentialsErrorMessage;
            }
            else if (context.CredentialsLoading)
            {
                insightBody = "The tray is requesting groups first, then credential entries, using the same active user and active namespace routing as the browser extension.";
            }
            else if (context.FilteredCredentialEntryCount == 0)
            {
                insightBody = string.IsNullOrWhiteSpace(context.SelectedCredentialGroupLabel)
                    ? "No credential entries are available for the current server."
                    : $"No credential entries matched the selected group: {context.SelectedCredentialGroupLabel}.";
            }
            else
            {
                string groupScope = string.IsNullOrWhiteSpace(context.SelectedCredentialGroupLabel)
                    ? "all groups"
                    : context.SelectedCredentialGroupLabel;
                insightBody = $"Showing {context.FilteredCredentialEntryCount} credential entr" +
                    (context.FilteredCredentialEntryCount == 1 ? "y" : "ies") +
                    $" for {groupScope}.";
            }

            return new TrayMenuViewState
            {
                Eyebrow = "CREDENTIALS",
                Title = "Credentials",
                Description = description,
                InsightTitle = "Collection state",
                InsightBody = insightBody,
                SelectionLayout = TrayMenuSelectionLayout.CredentialSelectors,
                AccountMetricOverride = $"{context.CredentialGroupCount} group" + (context.CredentialGroupCount == 1 ? string.Empty : "s"),
                ConnectionMetricOverride = $"{context.CredentialEntryCount} credential entr" + (context.CredentialEntryCount == 1 ? "y" : "ies"),
                UpdatedMetricOverride = context.CredentialsLoading ? "Loading..." : context.LastUpdatedText
            };
        }
    }
}