using VideoAnalysis.Core.Enums;

namespace VideoAnalysis.Core.Models;

public sealed record PlaylistItem(
    Guid Id,
    Guid PlaylistId,
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
