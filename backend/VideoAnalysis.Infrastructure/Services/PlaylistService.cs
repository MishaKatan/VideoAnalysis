using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Infrastructure.Services;

public sealed class PlaylistService : IPlaylistService
{
    private readonly IProjectRepository _repository;

    public PlaylistService(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<PlaylistDetailsDto> CreatePlaylistAsync(CreatePlaylistRequestDto request, CancellationToken cancellationToken)
    {
        if (request.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("Project id must be set.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Playlist name is required.", nameof(request));
        }

        if (request.PreRollFrames < 0 || request.PostRollFrames < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Roll frames must be >= 0.");
        }

        var project = await _repository.GetProjectAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException("Project was not found.");
        }

        var presetNames = (await _repository.GetTagPresetsAsync(request.ProjectId, cancellationToken))
            .ToDictionary((preset) => preset.Id, (preset) => preset.Name);

        var selectedEvents = await LoadSelectedEventsAsync(request.ProjectId, request.TagEventIds, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var playlist = new Playlist(
            Guid.NewGuid(),
            request.ProjectId,
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            now,
            now);

        var items = selectedEvents
            .Select((tagEvent, index) => BuildPlaylistItem(
                playlist.Id,
                tagEvent,
                index,
                request.PreRollFrames,
                request.PostRollFrames,
                request.MaxFrame,
                presetNames))
            .ToList();

        await _repository.UpsertPlaylistAsync(playlist, cancellationToken);
        await _repository.ReplacePlaylistItemsAsync(playlist.Id, items, cancellationToken);

        return MapDetails(playlist, items);
    }

    public async Task<PlaylistDetailsDto> AddEventsToPlaylistAsync(AddEventsToPlaylistRequestDto request, CancellationToken cancellationToken)
    {
        if (request.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("Project id must be set.", nameof(request));
        }

        if (request.PlaylistId == Guid.Empty)
        {
            throw new ArgumentException("Playlist id must be set.", nameof(request));
        }

        if (request.TagEventIds.Count == 0)
        {
            throw new ArgumentException("At least one event must be selected.", nameof(request));
        }

        var playlist = await _repository.GetPlaylistAsync(request.ProjectId, request.PlaylistId, cancellationToken);
        if (playlist is null)
        {
            throw new InvalidOperationException("Playlist was not found.");
        }

        var existingItems = (await _repository.GetPlaylistItemsAsync(request.PlaylistId, cancellationToken))
            .OrderBy((item) => item.SortOrder)
            .ToList();

        var existingEventIds = existingItems.Select((item) => item.TagEventId).ToHashSet();
        var selectedEvents = await LoadSelectedEventsAsync(request.ProjectId, request.TagEventIds, cancellationToken);
        var eventsToAppend = selectedEvents
            .Where((tagEvent) => !existingEventIds.Contains(tagEvent.Id))
            .ToList();

        var presetNames = (await _repository.GetTagPresetsAsync(request.ProjectId, cancellationToken))
            .ToDictionary((preset) => preset.Id, (preset) => preset.Name);

        var nextSortOrder = existingItems.Count;
        foreach (var tagEvent in eventsToAppend)
        {
            existingItems.Add(BuildPlaylistItem(
                playlist.Id,
                tagEvent,
                nextSortOrder++,
                0,
                0,
                request.MaxFrame,
                presetNames));
        }

        var updatedPlaylist = playlist with { UpdatedAtUtc = DateTimeOffset.UtcNow };
        await _repository.UpsertPlaylistAsync(updatedPlaylist, cancellationToken);
        await _repository.ReplacePlaylistItemsAsync(playlist.Id, existingItems, cancellationToken);

        return MapDetails(updatedPlaylist, existingItems);
    }

    public async Task<IReadOnlyList<PlaylistSummaryDto>> GetPlaylistsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var playlists = await _repository.GetPlaylistsAsync(projectId, cancellationToken);
        var summaries = new List<PlaylistSummaryDto>(playlists.Count);

        foreach (var playlist in playlists)
        {
            var itemCount = (await _repository.GetPlaylistItemsAsync(playlist.Id, cancellationToken)).Count;
            summaries.Add(new PlaylistSummaryDto(
                playlist.Id,
                playlist.ProjectId,
                playlist.Name,
                playlist.Description,
                itemCount,
                playlist.CreatedAtUtc,
                playlist.UpdatedAtUtc));
        }

        return summaries;
    }

    public async Task<PlaylistDetailsDto?> GetPlaylistAsync(Guid projectId, Guid playlistId, CancellationToken cancellationToken)
    {
        var playlist = await _repository.GetPlaylistAsync(projectId, playlistId, cancellationToken);
        if (playlist is null)
        {
            return null;
        }

        var items = await _repository.GetPlaylistItemsAsync(playlistId, cancellationToken);
        return MapDetails(playlist, items);
    }

    public async Task<IReadOnlyList<ClipSegmentDto>> GetClipSegmentsAsync(Guid projectId, Guid playlistId, CancellationToken cancellationToken)
    {
        var playlist = await _repository.GetPlaylistAsync(projectId, playlistId, cancellationToken);
        if (playlist is null)
        {
            return [];
        }

        var items = await _repository.GetPlaylistItemsAsync(playlistId, cancellationToken);
        return items
            .OrderBy((item) => item.SortOrder)
            .Select((item) => new ClipSegmentDto(item.TagEventId, item.ClipStartFrame, item.ClipEndFrame, item.Label, item.Player))
            .ToList();
    }

    public Task DeletePlaylistAsync(Guid projectId, Guid playlistId, CancellationToken cancellationToken)
    {
        return _repository.DeletePlaylistAsync(projectId, playlistId, cancellationToken);
    }

    private async Task<List<TagEvent>> LoadSelectedEventsAsync(Guid projectId, IReadOnlyList<Guid> tagEventIds, CancellationToken cancellationToken)
    {
        var selectedEventIds = tagEventIds.Distinct().ToHashSet();
        if (selectedEventIds.Count == 0)
        {
            return [];
        }

        var allEvents = await _repository.GetTagEventsAsync(
            projectId,
            new TagQuery(null, null, null, null, null, null),
            cancellationToken);

        var selectedEvents = allEvents
            .Where((tagEvent) => selectedEventIds.Contains(tagEvent.Id))
            .OrderBy((tagEvent) => tagEvent.StartFrame)
            .ThenBy((tagEvent) => tagEvent.EndFrame)
            .ToList();

        if (selectedEvents.Count != selectedEventIds.Count)
        {
            var missingIds = selectedEventIds.Except(selectedEvents.Select((tagEvent) => tagEvent.Id));
            throw new InvalidOperationException($"Some events were not found: {string.Join(", ", missingIds)}");
        }

        if (selectedEvents.Any((tagEvent) => tagEvent.IsOpen))
        {
            throw new InvalidOperationException("Only closed events can be added to a playlist.");
        }

        return selectedEvents;
    }

    private static PlaylistItem BuildPlaylistItem(
        Guid playlistId,
        TagEvent tagEvent,
        int sortOrder,
        int preRollFrames,
        int postRollFrames,
        long? maxFrame,
        IReadOnlyDictionary<Guid, string> presetNames)
    {
        var clipStartFrame = Math.Max(0, tagEvent.StartFrame - preRollFrames);
        var clipEndFrame = tagEvent.EndFrame + postRollFrames;

        if (maxFrame.HasValue)
        {
            clipEndFrame = Math.Min(maxFrame.Value, clipEndFrame);
        }

        if (clipEndFrame < clipStartFrame)
        {
            clipEndFrame = clipStartFrame;
        }

        return new PlaylistItem(
            Guid.NewGuid(),
            playlistId,
            tagEvent.Id,
            tagEvent.TagPresetId,
            sortOrder,
            tagEvent.StartFrame,
            tagEvent.EndFrame,
            clipStartFrame,
            clipEndFrame,
            preRollFrames,
            postRollFrames,
            presetNames.TryGetValue(tagEvent.TagPresetId, out var label) ? label : "Event",
            tagEvent.Player,
            tagEvent.TeamSide);
    }

    private static PlaylistDetailsDto MapDetails(Playlist playlist, IReadOnlyList<PlaylistItem> items)
    {
        return new PlaylistDetailsDto(
            playlist.Id,
            playlist.ProjectId,
            playlist.Name,
            playlist.Description,
            playlist.CreatedAtUtc,
            playlist.UpdatedAtUtc,
            items
                .OrderBy((item) => item.SortOrder)
                .Select((item) => new PlaylistItemDto(
                    item.Id,
                    item.TagEventId,
                    item.TagPresetId,
                    item.SortOrder,
                    item.EventStartFrame,
                    item.EventEndFrame,
                    item.ClipStartFrame,
                    item.ClipEndFrame,
                    item.PreRollFrames,
                    item.PostRollFrames,
                    item.Label,
                    item.Player,
                    item.TeamSide))
                .ToList());
    }
}
