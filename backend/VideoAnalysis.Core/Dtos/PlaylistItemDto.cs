using VideoAnalysis.Core.Enums;

namespace VideoAnalysis.Core.Dtos;

public sealed record PlaylistItemDto(
    Guid Id,
    Guid TagEventId,
    Guid TagPresetId,
    int SortOrder,
    long EventStartFrame,
    long EventEndFrame,
    long ClipStartFrame,
    long ClipEndFrame,
    int PreRollFrames,
    int PostRollFrames,
    string Label,
    string? Player,
    TeamSide TeamSide);
