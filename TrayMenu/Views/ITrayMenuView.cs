namespace TrayApp.TrayMenu.Views
{
    internal interface ITrayMenuView
    {
        TrayMenuViewState Build(TrayMenuContext context);
    }
}