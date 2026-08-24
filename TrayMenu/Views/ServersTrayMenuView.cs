namespace TrayApp.TrayMenu.Views
{
    internal sealed class ServersTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            string serverList = context.RegisteredServers.Count > 0
                ? string.Join(", ", context.RegisteredServers)
                : "No registered servers.";

            return new TrayMenuViewState
            {
                Eyebrow = "SERVERS",
                Title = "Registered servers",
                Description = $"Active server: {context.ActiveServerLabel}",
                InsightTitle = "Server list",
                InsightBody = $"{serverList}\n\nUse Register new server to start namespace pairing for the active session.",
                SelectionLayout = TrayMenuSelectionLayout.ServerSelector,
                ConnectionMetricOverride = $"{context.RegisteredServerCount} server entr" + (context.RegisteredServerCount == 1 ? "y" : "ies")
            };
        }
    }
}