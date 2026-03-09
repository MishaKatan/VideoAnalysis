namespace VideoAnalysis.Core.Dtos;

public sealed record TagEventDto(
    Guid Id,
    Guid TagPresetId,
    string PresetName,
    long StartFrame,
    long EndFrame,
    string? Player,
    string? Period,
    string? Notes);
