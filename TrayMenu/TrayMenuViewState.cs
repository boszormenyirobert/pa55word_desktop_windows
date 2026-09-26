namespace TrayApp.TrayMenu
{
    internal sealed class RegisteredServerCardViewState
    {
        public string UserId { get; init; } = string.Empty;

        public string NamespaceId { get; init; } = string.Empty;

        public string NamespaceTitle { get; init; } = string.Empty;

        public bool HasSession { get; init; }

        public bool IsRevoking { get; init; }

        public string UserLabel { get; init; } = string.Empty;

        public string UserValue { get; init; } = string.Empty;

        public string DeviceLabel { get; init; } = string.Empty;

        public string DeviceValue { get; init; } = string.Empty;

        public string UrlLabel { get; init; } = string.Empty;

        public string UrlValue { get; init; } = string.Empty;
    }

    internal sealed class TrayMenuViewState
    {
        public string Eyebrow { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string InsightTitle { get; init; } = string.Empty;

        public string InsightBody { get; init; } = string.Empty;

        public TrayMenuSelectionLayout SelectionLayout { get; init; }

        public bool ShowUserSelector { get; init; } = true;

        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();

        public string? AccountMetricOverride { get; init; }

        public string? ConnectionMetricOverride { get; init; }

        public string? UpdatedMetricOverride { get; init; }

        public int? InsightCardHeightOverride { get; init; }

        public int? InsightBodyHeightOverride { get; init; }

        public bool HideMetricsCard { get; init; }

        public bool HideInsightTitle { get; init; }

        public IReadOnlyList<RegisteredServerCardViewState> RegisteredServerCards { get; init; } = Array.Empty<RegisteredServerCardViewState>();
    }
}