using TrayApp.TrayMenu;
using Xunit;

namespace TrayApp.Tests;

public sealed class TrayMenuViewTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void LoginView_ShowsUserSelectorOnlyForMultipleUsers(int registeredUserCount, bool expectedUserSelector)
    {
        var context = new TrayMenuContext { RegisteredUserCount = registeredUserCount };

        TrayMenuViewState viewState = TrayMenuViews.Build(TrayMenuSection.Login, context);

        Assert.Equal(TrayMenuSelectionLayout.LoginSelectors, viewState.SelectionLayout);
        Assert.Equal(expectedUserSelector, viewState.ShowUserSelector);
    }

    [Fact]
    public void LoginView_ShowsAuthorizationErrorInDescription()
    {
        var context = new TrayMenuContext
        {
            LoginErrorMessage = Services.AuthenticationService.NoDevicesMessage
        };

        TrayMenuViewState viewState = TrayMenuViews.Build(TrayMenuSection.Login, context);

        Assert.Equal(
            "There is no mobile device registered to this account that can authorize the request.",
            viewState.Description);
    }

    [Fact]
    public void LoginView_WithoutAuthorizationError_PreservesDefaultDescription()
    {
        TrayMenuViewState viewState = TrayMenuViews.Build(TrayMenuSection.Login, new TrayMenuContext());

        Assert.Equal("Choose the account and registered server you want to use.", viewState.Description);
    }

    [Fact]
    public void SettingsViews_ExposeTheirExpectedActionLayouts()
    {
        TrayMenuViewState usersViewState = TrayMenuViews.Build(TrayMenuSection.SettingsUsers, new TrayMenuContext());
        TrayMenuViewState serversViewState = TrayMenuViews.Build(TrayMenuSection.SettingsServers, new TrayMenuContext());

        Assert.Equal(TrayMenuSelectionLayout.SettingsUserActions, usersViewState.SelectionLayout);
        Assert.Equal(TrayMenuSelectionLayout.SettingsServerActions, serversViewState.SelectionLayout);
    }

    [Fact]
    public void SettingsOverview_UsesItsDedicatedLayout()
    {
        TrayMenuViewState viewState = TrayMenuViews.Build(TrayMenuSection.Settings, new TrayMenuContext());

        Assert.Equal("Settings", viewState.Title);
        Assert.Equal(TrayMenuSelectionLayout.None, viewState.SelectionLayout);
    }

    [Fact]
    public void UnknownSection_PreservesLoginFallback()
    {
        TrayMenuViewState viewState = TrayMenuViews.Build(
            (TrayMenuSection)(-1), new TrayMenuContext { RegisteredUserCount = 2 });

        Assert.Equal("LOGIN", viewState.Eyebrow);
        Assert.Equal("Select account and server", viewState.Title);
        Assert.Equal(TrayMenuSelectionLayout.LoginSelectors, viewState.SelectionLayout);
        Assert.True(viewState.ShowUserSelector);
    }

    [Theory]
    [InlineData((int)TrayMenuSection.Settings, "Settings", (int)TrayMenuSelectionLayout.None)]
    [InlineData((int)TrayMenuSection.SettingsUsers, "Profile", (int)TrayMenuSelectionLayout.SettingsUserActions)]
    [InlineData((int)TrayMenuSection.SettingsServers, "Servers", (int)TrayMenuSelectionLayout.SettingsServerActions)]
    [InlineData((int)TrayMenuSection.Servers, "Servers", (int)TrayMenuSelectionLayout.SettingsServerActions)]
    [InlineData((int)TrayMenuSection.Devices, "Registered devices for the user profile: Alice", (int)TrayMenuSelectionLayout.HomeSummary)]
    [InlineData((int)TrayMenuSection.Accounts, "User account", (int)TrayMenuSelectionLayout.HomeSummary)]
    [InlineData((int)TrayMenuSection.Faq, "Help and FAQ", (int)TrayMenuSelectionLayout.HomeSummary)]
    public void StaticSections_PreserveTitlesLayoutsAndEmptyInsights(
        int section, string title, int layout)
    {
        TrayMenuViewState viewState = TrayMenuViews.Build((TrayMenuSection)section,
            new TrayMenuContext { ActiveUserLabel = "Alice" });

        Assert.Equal(title, viewState.Title);
        Assert.Equal((TrayMenuSelectionLayout)layout, viewState.SelectionLayout);
        Assert.Equal(string.Empty, viewState.InsightTitle);
        Assert.Equal(string.Empty, viewState.InsightBody);
        Assert.True(viewState.ShowUserSelector);
    }

    [Theory]
    [InlineData(false, "", 0, "", "No credential entries are available for the current server.")]
    [InlineData(false, "", 0, "Work", "No credential entries matched the selected group: Work.")]
    [InlineData(false, "", 1, "", "Showing 1 credential entry for all groups.")]
    [InlineData(false, "", 2, "Work", "Showing 2 credential entries for Work.")]
    [InlineData(true, "Request failed.", 2, "Work", "Request failed.")]
    public void CredentialsView_PreservesCollectionMessagesAndMetrics(
        bool loading, string error, int count, string group, string expectedInsight)
    {
        var context = new TrayMenuContext
        {
            CredentialsLoading = loading,
            CredentialsErrorMessage = error,
            FilteredCredentialEntryCount = count,
            SelectedCredentialGroupLabel = group,
            CredentialGroupCount = 1,
            CredentialEntryCount = count,
            LastUpdatedText = "20:00"
        };

        TrayMenuViewState viewState = TrayMenuViews.Build(TrayMenuSection.Credentials, context);

        Assert.Equal(TrayMenuSelectionLayout.CredentialSelectors, viewState.SelectionLayout);
        Assert.Equal(expectedInsight, viewState.InsightBody);
        Assert.Equal("1 group", viewState.AccountMetricOverride);
        Assert.Equal($"{count} credential " + (count == 1 ? "entry" : "entries"),
            viewState.ConnectionMetricOverride);
        Assert.Equal(loading ? "Loading..." : "20:00", viewState.UpdatedMetricOverride);
    }

    [Fact]
    public void CredentialsView_LoadingWithoutError_PreservesLoadingGuidance()
    {
        TrayMenuViewState viewState = TrayMenuViews.Build(TrayMenuSection.Credentials,
            new TrayMenuContext { CredentialsLoading = true, CredentialGroupCount = 2 });

        Assert.Equal("Loading credential groups and entries for the active server.", viewState.Description);
        Assert.Equal("The tray is requesting groups first, then credential entries, using the same active user and active namespace routing as the browser extension.",
            viewState.InsightBody);
        Assert.Equal("2 groups", viewState.AccountMetricOverride);
    }

    [Fact]
    public void TrayMenuController_StartsOnLogin()
    {
        var controller = new TrayMenuController((_, _) => { }, () => { });

        Assert.Equal(TrayMenuSection.Login, controller.ActiveSection);
    }
}