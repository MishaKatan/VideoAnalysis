using VideoAnalysis.Core.Enums;

namespace VideoAnalysis.Core.Models;

public sealed record TagEvent(
    Guid Id,
    Guid ProjectId,
    Guid TagPresetId,
    long StartFrame,
    long EndFrame,
    string? Player,
    string? Period,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    TeamSide TeamSide = TeamSide.Unknown,
    bool IsOpen = false);
