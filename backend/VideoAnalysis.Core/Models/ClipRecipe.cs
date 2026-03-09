namespace VideoAnalysis.Core.Models;

public sealed record ClipRecipe(
    Guid Id,
    Guid ProjectId,
    string Name,
    Guid? TagPresetId,
    string? Player,
    string? Period,
    string? QueryText,
    int PreRollFrames,
    int PostRollFrames,
    DateTimeOffset CreatedAtUtc);
