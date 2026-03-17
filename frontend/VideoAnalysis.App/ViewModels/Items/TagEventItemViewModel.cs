namespace VideoAnalysis.App.ViewModels.Items;

public sealed class TagEventItemViewModel
{
    public required Guid Id { get; init; }
    public required Guid TagPresetId { get; init; }
    public required string PresetName { get; init; }
    public required string TeamSide { get; init; }
    public required long StartFrame { get; init; }
    public required long EndFrame { get; init; }
    public required string Player { get; init; }
    public required string Period { get; init; }
    public required string Notes { get; init; }
}
