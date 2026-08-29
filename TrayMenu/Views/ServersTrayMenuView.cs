namespace TrayApp.TrayMenu.Views
{
    internal sealed class ServersTrayMenuView : ITrayMenuView
    {
        public TrayMenuViewState Build(TrayMenuContext context)
        {
            int serverCount = context.RegisteredServerDetails.Count;
            string serverList = serverCount > 0 ? string.Empty : "No registered servers.";
            int insightBodyHeight = serverCount > 0
                ? Math.Clamp(serverCount * 150, 56, 330)
                : 56;

            return new TrayMenuViewState
            {
                Eyebrow = "SERVERS",
                Title = "Registered servers",
                Description = $"All namespaces registered under {context.ActiveUserLabel}.",
                InsightTitle = string.Empty,
                InsightBody = serverList,
                SelectionLayout = TrayMenuSelectionLayout.ServerSelector,
                InsightCardHeightOverride = insightBodyHeight + 34,
                InsightBodyHeightOverride = insightBodyHeight,
                HideMetricsCard = true,
                HideInsightTitle = true,
                RegisteredServerCards = context.RegisteredServerDetails
                    .Select(item => new RegisteredServerCardViewState
                    {
                        UserId = item.UserId,
                        NamespaceId = item.NamespaceId,
                        NamespaceTitle = item.Namespace,
                        HasSession = item.HasSession,
                        IsRevoking = false,
                        UserLabel = "User",
                        UserValue = item.UserName,
                        DeviceLabel = "device",
                        DeviceValue = item.DeviceName,
                        UrlLabel = "URL",
                        UrlValue = item.Url
                    })
                    .ToArray()
            };
        }
    }
}