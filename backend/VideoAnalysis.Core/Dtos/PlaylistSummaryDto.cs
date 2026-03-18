namespace VideoAnalysis.Core.Dtos;

public sealed record PlaylistSummaryDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    int ItemCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
