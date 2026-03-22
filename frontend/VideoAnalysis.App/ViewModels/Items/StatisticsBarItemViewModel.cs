namespace VideoAnalysis.App.ViewModels.Items;

public sealed class StatisticsBarItemViewModel
{
    public required string Name { get; init; }
    public required int HomeCount { get; init; }
    public required int AwayCount { get; init; }
    public int MaxCount => Math.Max(1, Math.Max(HomeCount, AwayCount));
}
