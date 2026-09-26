namespace TrayApp.TrayMenu
{
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

    }
}