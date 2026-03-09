namespace VideoAnalysis.Core.Models;

public sealed record TagQuery(
    Guid? TagPresetId,
    string? Player,
    string? Period,
    string? Text);
