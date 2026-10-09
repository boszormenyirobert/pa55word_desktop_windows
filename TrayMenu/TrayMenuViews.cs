namespace TrayApp.TrayMenu
{
    internal static class TrayMenuViews
    {
        public static TrayMenuViewState Build(TrayMenuSection section, TrayMenuContext context)
        {
            return section switch
            {
                TrayMenuSection.Settings => new TrayMenuViewState
                {
                    Eyebrow = "SETTINGS",
                    Title = "Settings",
                    Description = "Manage your profile, connected databases, registered devices, and account details from one place.",
                    SelectionLayout = TrayMenuSelectionLayout.None
                },
                TrayMenuSection.SettingsUsers => new TrayMenuViewState
                {
                    Eyebrow = "SETTINGS",
                    Title = "Profile",
                    Description = "Choose a profile action.",
                    SelectionLayout = TrayMenuSelectionLayout.SettingsUserActions
                },
                TrayMenuSection.SettingsServers or TrayMenuSection.Servers => new TrayMenuViewState
                {
                    Eyebrow = "SETTINGS",
                    Title = "Manage Databases",
                    Description = "Add a database or grant this desktop access to an existing database.",
                    SelectionLayout = TrayMenuSelectionLayout.SettingsServerActions
                },
                TrayMenuSection.Devices => new TrayMenuViewState
                {
                    Eyebrow = "SETTINGS",
                    Title = $"Registered devices for the user profile: {context.ActiveUserLabel}",
                    Description = "Review the guidance before replacing or deleting a device.",
                    SelectionLayout = TrayMenuSelectionLayout.HomeSummary
                },
                TrayMenuSection.Accounts => new TrayMenuViewState
                {
                    Eyebrow = "ACCOUNTS",
                    Title = "User account",
                    Description = "Deleting an account is an irreversible process.",
                    SelectionLayout = TrayMenuSelectionLayout.HomeSummary
                },
                TrayMenuSection.Credentials => BuildCredentials(context),
                TrayMenuSection.Faq => new TrayMenuViewState
                {
                    Eyebrow = "SUPPORT",
                    Title = "Help and FAQ",
                    Description = "Registration, databases, profiles, credentials, and device access.",
                    SelectionLayout = TrayMenuSelectionLayout.HomeSummary
                },
                _ => new TrayMenuViewState
                {
                    Eyebrow = "LOGIN",
                    Title = "Select account and server",
                    Description = string.IsNullOrWhiteSpace(context.LoginErrorMessage)
                        ? "Choose the account and registered server you want to use."
                        : context.LoginErrorMessage,
                    SelectionLayout = TrayMenuSelectionLayout.LoginSelectors,
                    ShowUserSelector = context.RegisteredUserCount > 1
                }
            };
        }

        private static TrayMenuViewState BuildCredentials(TrayMenuContext context)
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