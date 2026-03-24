using VideoAnalysis.Core.Dtos;

namespace VideoAnalysis.Core.Abstractions;

public interface IPlaylistService
{
    Task<PlaylistDetailsDto> CreatePlaylistAsync(CreatePlaylistRequestDto request, CancellationToken cancellationToken);
    Task<PlaylistDetailsDto> AddEventsToPlaylistAsync(AddEventsToPlaylistRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlaylistSummaryDto>> GetPlaylistsAsync(Guid projectId, CancellationToken cancellationToken);
    Task<PlaylistDetailsDto?> GetPlaylistAsync(Guid projectId, Guid playlistId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClipSegmentDto>> GetClipSegmentsAsync(Guid projectId, Guid playlistId, CancellationToken cancellationToken);
    Task DeletePlaylistAsync(Guid projectId, Guid playlistId, CancellationToken cancellationToken);
}

