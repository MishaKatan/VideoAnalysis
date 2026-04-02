using VideoAnalysis.Core.Enums;

namespace VideoAnalysis.Core.Dtos;

public sealed record ClipSegmentDto(
    Guid TagEventId,
    long StartFrame,
    long EndFrame,
    string Label,
    string? Player,
    TeamSide TeamSide = TeamSide.Unknown,
    string? TeamName = null,
    string? Period = null,
    string? MatchClockText = null,
    string? AccentColorHex = null,
    string? CounterText = null);
