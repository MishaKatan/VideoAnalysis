using VideoAnalysis.Core.Enums;

namespace VideoAnalysis.Core.Dtos;

public sealed record TagEventDto(
    Guid Id,
    Guid TagPresetId,
    string PresetName,
    long StartFrame,
    long EndFrame,
    string? Player,
    string? Period,
    string? Notes,
    TeamSide TeamSide = TeamSide.Unknown,
    bool IsOpen = false,
    string? PresetHotkey = null,
    string? PresetIconKey = null,
    string? PresetColorHex = null);
