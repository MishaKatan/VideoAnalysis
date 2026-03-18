namespace VideoAnalysis.App.ViewModels.Items;

public sealed class PlaylistSummaryItemViewModel
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int ItemCount { get; init; }
    public required string UpdatedAtText { get; init; }
}
