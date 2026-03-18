namespace VideoAnalysis.Core.Dtos;

public sealed record PlaylistDetailsDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<PlaylistItemDto> Items);
