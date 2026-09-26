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
        var registry = new TrayMenuViewRegistry();
        var context = new TrayMenuContext { RegisteredUserCount = registeredUserCount };

        TrayMenuViewState viewState = registry.Build(TrayMenuSection.Login, context);

        Assert.Equal(TrayMenuSelectionLayout.LoginSelectors, viewState.SelectionLayout);
        Assert.Equal(expectedUserSelector, viewState.ShowUserSelector);
    }

    [Fact]
    public void SettingsViews_ExposeTheirExpectedActionLayouts()
    {
        var registry = new TrayMenuViewRegistry();

        TrayMenuViewState usersViewState = registry.Build(TrayMenuSection.SettingsUsers, new TrayMenuContext());
        TrayMenuViewState serversViewState = registry.Build(TrayMenuSection.SettingsServers, new TrayMenuContext());

        Assert.Equal(TrayMenuSelectionLayout.SettingsUserActions, usersViewState.SelectionLayout);
        Assert.Equal(TrayMenuSelectionLayout.SettingsServerActions, serversViewState.SelectionLayout);
    }

    [Fact]
    public void TrayMenuController_StartsOnLogin()
    {
        var controller = new TrayMenuController((_, _) => { }, () => { });

        Assert.Equal(TrayMenuSection.Login, controller.ActiveSection);
    }
}