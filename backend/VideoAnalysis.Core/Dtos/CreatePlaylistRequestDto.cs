namespace VideoAnalysis.Core.Dtos;

public sealed record CreatePlaylistRequestDto(
    Guid ProjectId,
    string Name,
    IReadOnlyList<Guid> TagEventIds,
    int PreRollFrames = 0,
    int PostRollFrames = 0,
    string? Description = null,
    long? MaxFrame = null);
