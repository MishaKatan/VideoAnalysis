using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Core.Enums;
using VideoAnalysis.Core.Models;
using VideoAnalysis.Infrastructure.Persistence;
using VideoAnalysis.Infrastructure.Services;

namespace VideoAnalysis.Tests;

public sealed class PlaylistServiceTests : IDisposable
{
    private readonly string _dbPath;

    public PlaylistServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "video-analysis-playlist-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
    }

    [Fact]
    public async Task CreatePlaylistAsync_CreatesStableSegmentsWithRolls()
    {
        var repository = new SqliteProjectRepository(_dbPath);
        await repository.InitializeAsync(CancellationToken.None);

        var projectId = Guid.NewGuid();
        var goalPresetId = Guid.NewGuid();
        var shotPresetId = Guid.NewGuid();

        await repository.CreateProjectAsync(
            new Project(projectId, "Playlists", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "Home", "Away", "C:\\Projects\\playlists"),
            CancellationToken.None);

        await repository.UpsertTagPresetAsync(
            new TagPreset(goalPresetId, projectId, "Goal", "#E53935", "GameEvent", false, "G", "goal"),
            CancellationToken.None);
        await repository.UpsertTagPresetAsync(
            new TagPreset(shotPresetId, projectId, "Shot", "#1E88E5", "GameEvent", false, "S", "shot"),
            CancellationToken.None);

        var secondEventId = Guid.NewGuid();
        await repository.UpsertTagEventAsync(
            new TagEvent(secondEventId, projectId, shotPresetId, 220, 240, "Player B", "2", null, DateTimeOffset.UtcNow, TeamSide.Away, false),
            CancellationToken.None);

        var firstEventId = Guid.NewGuid();
        await repository.UpsertTagEventAsync(
            new TagEvent(firstEventId, projectId, goalPresetId, 100, 120, "Player A", "1", null, DateTimeOffset.UtcNow, TeamSide.Home, false),
            CancellationToken.None);

        var service = new PlaylistService(repository);
        var playlist = await service.CreatePlaylistAsync(
            new CreatePlaylistRequestDto(
                projectId,
                "Top moments",
                [secondEventId, firstEventId],
                PreRollFrames: 15,
                PostRollFrames: 25,
                Description: "Sorted by timeline",
                MaxFrame: 230),
            CancellationToken.None);

        Assert.Equal("Top moments", playlist.Name);
        Assert.Equal(2, playlist.Items.Count);
        Assert.Equal(firstEventId, playlist.Items[0].TagEventId);
        Assert.Equal(85, playlist.Items[0].ClipStartFrame);
        Assert.Equal(145, playlist.Items[0].ClipEndFrame);
        Assert.Equal("Goal", playlist.Items[0].Label);

        Assert.Equal(secondEventId, playlist.Items[1].TagEventId);
        Assert.Equal(205, playlist.Items[1].ClipStartFrame);
        Assert.Equal(230, playlist.Items[1].ClipEndFrame);
        Assert.Equal("Shot", playlist.Items[1].Label);

        var segments = await service.GetClipSegmentsAsync(projectId, playlist.Id, CancellationToken.None);
        Assert.Equal(2, segments.Count);
        Assert.Equal(85, segments[0].StartFrame);
        Assert.Equal(230, segments[1].EndFrame);
    }

    [Fact]
    public async Task CreatePlaylistAsync_RejectsOpenEvents()
    {
        var repository = new SqliteProjectRepository(_dbPath);
        await repository.InitializeAsync(CancellationToken.None);

        var projectId = Guid.NewGuid();
        var presetId = Guid.NewGuid();

        await repository.CreateProjectAsync(
            new Project(projectId, "Playlists", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null, "C:\\Projects\\playlists"),
            CancellationToken.None);

        await repository.UpsertTagPresetAsync(
            new TagPreset(presetId, projectId, "Goal", "#E53935", "GameEvent", false, "G", "goal"),
            CancellationToken.None);

        var openEventId = Guid.NewGuid();
        await repository.UpsertTagEventAsync(
            new TagEvent(openEventId, projectId, presetId, 100, 100, null, null, null, DateTimeOffset.UtcNow, TeamSide.Unknown, true),
            CancellationToken.None);

        var service = new PlaylistService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePlaylistAsync(
                new CreatePlaylistRequestDto(projectId, "Broken", [openEventId]),
                CancellationToken.None));
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
