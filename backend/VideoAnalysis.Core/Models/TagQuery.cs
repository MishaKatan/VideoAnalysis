using VideoAnalysis.Core.Enums;

namespace VideoAnalysis.Core.Models;

public sealed record TagQuery(
    Guid? TagPresetId,
    string? Player,
    string? Period,
    string? Text,
    TeamSide? TeamSide = null,
    bool? IsOpen = null);
