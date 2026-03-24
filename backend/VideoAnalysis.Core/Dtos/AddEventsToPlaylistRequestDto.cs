namespace VideoAnalysis.Core.Dtos;

public sealed record AddEventsToPlaylistRequestDto(
    Guid ProjectId,
    Guid PlaylistId,
    IReadOnlyList<Guid> TagEventIds,
    long? MaxFrame = null);
