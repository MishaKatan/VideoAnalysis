namespace VideoAnalysis.Core.Dtos;

public sealed record ClipSegmentDto(
    Guid TagEventId,
    long StartFrame,
    long EndFrame,
    string Label,
    string? Player);
