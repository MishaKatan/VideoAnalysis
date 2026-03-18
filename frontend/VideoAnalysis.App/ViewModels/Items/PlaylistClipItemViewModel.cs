namespace VideoAnalysis.App.ViewModels.Items;

public sealed class PlaylistClipItemViewModel
{
    public required Guid Id { get; init; }
    public required Guid TagEventId { get; init; }
    public required string Label { get; init; }
    public required string Player { get; init; }
    public required string TeamSide { get; init; }
    public required long ClipStartFrame { get; init; }
    public required long ClipEndFrame { get; init; }
    public required string FrameRangeText { get; init; }
}
