namespace VideoAnalysis.Core.Models;

public sealed record Playlist(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
