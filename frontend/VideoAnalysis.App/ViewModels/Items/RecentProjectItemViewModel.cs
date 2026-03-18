namespace VideoAnalysis.App.ViewModels.Items;

public sealed class RecentProjectItemViewModel
{
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Matchup { get; init; }
    public required string Summary { get; init; }
    public required string UpdatedAtText { get; init; }
}
