using VideoAnalysis.App.Configuration;
using VideoAnalysis.App.ViewModels.Shell;
using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Core.Enums;
using VideoAnalysis.Core.Models;
using VideoAnalysis.Core.Services;
using VideoAnalysis.Infrastructure.Persistence;
using VideoAnalysis.Infrastructure.Services;

namespace VideoAnalysis.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly string _tempRootPath;
    private readonly string _settingsPath;
    private readonly string _projectsRootPath;
    private readonly string _sourceVideoPath;

    public MainWindowViewModelTests()
    {
        _tempRootPath = Path.Combine(Path.GetTempPath(), "video-analysis-vm-tests", Guid.NewGuid().ToString("N"));
        _settingsPath = Path.Combine(_tempRootPath, "settings.json");
        _projectsRootPath = Path.Combine(_tempRootPath, "projects");
        Directory.CreateDirectory(_tempRootPath);
        Directory.CreateDirectory(_projectsRootPath);

        _sourceVideoPath = CreateSourceVideoFile("match.mp4");
    }

    [Fact]
    public async Task ContinueNewProjectCommand_CreatesProjectAndLoadsImportedVideo()
    {
        var repository = new SqliteProjectRepository(_projectsRootPath);
        var projectSetupService = new ProjectSetupService(repository, _projectsRootPath);
        var mediaPlaybackService = new FakeMediaPlaybackService();
        var viewModel = new MainWindowViewModel(
            repository,
            projectSetupService,
            new PlaylistService(repository),
            new TagService(),
            new FakeClipComposerService(),
            new FakeExportService(),
            mediaPlaybackService,
            new AppSettingsStore(_settingsPath),
            new AppSettings());

        viewModel.NewProjectName = "Integration Match";
        viewModel.NewProjectDescription = "Playoffs";
        viewModel.NewProjectHomeTeam = "Home";
        viewModel.NewProjectAwayTeam = "Away";
        viewModel.NewProjectVideoPath = _sourceVideoPath;
        viewModel.IsNewProjectDialogOpen = true;

        await viewModel.ContinueNewProjectCommand.ExecuteAsync(null);

        var projects = await repository.ListProjectsAsync(CancellationToken.None);
        var project = Assert.Single(projects);
        var projectVideo = await repository.GetProjectVideoAsync(project.Id, CancellationToken.None);

        Assert.False(viewModel.IsNewProjectDialogOpen);
        Assert.Equal(project.Name, viewModel.ProjectName);
        Assert.NotNull(projectVideo);
        Assert.Equal(projectVideo!.StoredFilePath, viewModel.SourceVideoPath);
        Assert.True(File.Exists(_sourceVideoPath));
        Assert.True(File.Exists(projectVideo.StoredFilePath));
        Assert.Contains($"{Path.DirectorySeparatorChar}media{Path.DirectorySeparatorChar}", projectVideo.StoredFilePath);
        Assert.Equal(25, viewModel.FramesPerSecond);
        Assert.Equal(250, viewModel.DurationFrames);
        Assert.NotEmpty(viewModel.TagPresets);
        Assert.False(viewModel.IsStartupScreenOpen);
        Assert.Contains("created", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitializeCommand_WithExistingProjects_ShowsStartupScreenAndRecentProjects()
    {
        var repository = new SqliteProjectRepository(_projectsRootPath);
        var projectSetupService = new ProjectSetupService(repository, _projectsRootPath);
        await projectSetupService.CreateProjectWithVideoAsync(
            new CreateProjectRequestDto("Existing Match", CreateSourceVideoFile("existing.mp4"), "Existing Match"),
            CancellationToken.None);

        var viewModel = CreateViewModel(repository, projectSetupService, new FakeMediaPlaybackService());

        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsStartupScreenOpen);
        Assert.True(viewModel.HasRecentProjects);
        Assert.NotNull(viewModel.SelectedRecentProject);
        Assert.Equal(string.Empty, viewModel.SourceVideoPath);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
    }

    [Fact]
    public async Task OpenSelectedRecentProjectCommand_LoadsProjectFromStartupScreen()
    {
        var repository = new SqliteProjectRepository(_projectsRootPath);
        var projectSetupService = new ProjectSetupService(repository, _projectsRootPath);

        await projectSetupService.CreateProjectWithVideoAsync(
            new CreateProjectRequestDto("First Match", CreateSourceVideoFile("first.mp4"), "First Match"),
            CancellationToken.None);
        await projectSetupService.CreateProjectWithVideoAsync(
            new CreateProjectRequestDto("Second Match", CreateSourceVideoFile("second.mp4"), "Second Match"),
            CancellationToken.None);

        var viewModel = CreateViewModel(repository, projectSetupService, new FakeMediaPlaybackService());
        await viewModel.InitializeCommand.ExecuteAsync(null);

        viewModel.SelectedRecentProject = Assert.Single(viewModel.RecentProjects, (project) => project.Name == "Second Match");
        await viewModel.OpenSelectedRecentProjectCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsStartupScreenOpen);
        Assert.Equal("Second Match", viewModel.ProjectName);
        Assert.NotEmpty(viewModel.TagPresets);
        Assert.NotEqual(string.Empty, viewModel.SourceVideoPath);
        Assert.Contains("opened", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePlaylistCommand_CreatesPlaylistAndLoadsPlaylistItems()
    {
        var repository = new SqliteProjectRepository(_projectsRootPath);
        var projectSetupService = new ProjectSetupService(repository, _projectsRootPath);

        await projectSetupService.CreateProjectWithVideoAsync(
            new CreateProjectRequestDto("Playlist Match", CreateSourceVideoFile("playlist.mp4"), "Playlist Match"),
            CancellationToken.None);

        var viewModel = CreateViewModel(repository, projectSetupService, new FakeMediaPlaybackService());
        await viewModel.InitializeCommand.ExecuteAsync(null);
        viewModel.SelectedRecentProject = Assert.Single(viewModel.RecentProjects, project => project.Name == "Playlist Match");
        await viewModel.OpenSelectedRecentProjectCommand.ExecuteAsync(null);

        var preset = viewModel.TagPresets.First((candidate) => candidate.IconKey == "goal");
        await repository.UpsertTagEventAsync(
            new TagEvent(Guid.NewGuid(), viewModel.RecentProjects[0].ProjectId, preset.Id, 100, 130, "Player A", "1", null, DateTimeOffset.UtcNow, TeamSide.Home, false),
            CancellationToken.None);
        await repository.UpsertTagEventAsync(
            new TagEvent(Guid.NewGuid(), viewModel.RecentProjects[0].ProjectId, preset.Id, 220, 250, "Player B", "2", null, DateTimeOffset.UtcNow, TeamSide.Away, false),
            CancellationToken.None);

        await viewModel.OpenStartupScreenCommand.ExecuteAsync(null);
        viewModel.SelectedRecentProject = Assert.Single(viewModel.RecentProjects, project => project.Name == "Playlist Match");
        await viewModel.OpenSelectedRecentProjectCommand.ExecuteAsync(null);

        viewModel.TogglePlaylistSelectionCommand.Execute(viewModel.TagEvents[0]);
        viewModel.TogglePlaylistSelectionCommand.Execute(viewModel.TagEvents[1]);
        viewModel.PlaylistName = "Goals playlist";
        viewModel.PreRollFrames = 10;
        viewModel.PostRollFrames = 20;

        await viewModel.CreatePlaylistCommand.ExecuteAsync(null);

        var playlists = await repository.GetPlaylistsAsync(viewModel.RecentProjects[0].ProjectId, CancellationToken.None);
        var playlist = Assert.Single(playlists);
        var playlistItems = await repository.GetPlaylistItemsAsync(playlist.Id, CancellationToken.None);

        Assert.Single(viewModel.Playlists);
        Assert.Equal(2, viewModel.PlaylistItems.Count);
        Assert.Equal("Goals playlist", playlist.Name);
        Assert.Equal(90, playlistItems[0].ClipStartFrame);
        Assert.Equal(250, playlistItems[1].ClipEndFrame);
        Assert.Contains("Goals playlist", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleEventTypeHotkeyAsync_Twice_SavesClosedEvent()
    {
        var repository = new SqliteProjectRepository(_projectsRootPath);
        var projectSetupService = new ProjectSetupService(repository, _projectsRootPath);

        await projectSetupService.CreateProjectWithVideoAsync(
            new CreateProjectRequestDto("Hotkey Match", CreateSourceVideoFile("hotkey.mp4"), "Hotkey Match"),
            CancellationToken.None);

        var viewModel = CreateViewModel(repository, projectSetupService, new FakeMediaPlaybackService());
        await viewModel.InitializeCommand.ExecuteAsync(null);
        viewModel.SelectedRecentProject = Assert.Single(viewModel.RecentProjects, project => project.Name == "Hotkey Match");
        await viewModel.OpenSelectedRecentProjectCommand.ExecuteAsync(null);

        var preset = viewModel.TagPresets.First(candidate => string.Equals(candidate.Hotkey, "G", StringComparison.OrdinalIgnoreCase));

        viewModel.CurrentFrame = 100;
        await viewModel.HandleEventTypeHotkeyAsync("G");

        Assert.True(viewModel.IsTagEventEditorOpen);
        Assert.Equal(preset.Id, viewModel.SelectedPreset?.Id);
        Assert.Equal(100, viewModel.TagEndFrame);

        viewModel.CurrentFrame = 135;
        await viewModel.HandleEventTypeHotkeyAsync("G");

        Assert.False(viewModel.IsTagEventEditorOpen);

        var savedEvents = await repository.GetTagEventsAsync(
            viewModel.SelectedRecentProject!.ProjectId,
            new TagQuery(preset.Id, null, null, null, null, false),
            CancellationToken.None);

        var savedEvent = Assert.Single(savedEvents);
        Assert.Equal(100, savedEvent.StartFrame);
        Assert.Equal(135 + preset.PostRollFrames, savedEvent.EndFrame);
        Assert.Equal(TeamSide.Home, savedEvent.TeamSide);
    }

    [Fact]
    public async Task ExportCommand_EnrichesSegmentsWithEventOverlayMetadata()
    {
        var repository = new SqliteProjectRepository(_projectsRootPath);
        var projectSetupService = new ProjectSetupService(repository, _projectsRootPath);

        await projectSetupService.CreateProjectWithVideoAsync(
            new CreateProjectRequestDto("Overlay Match", CreateSourceVideoFile("overlay.mp4"), "Overlay Match", "Playoffs", "Молот", "Химик"),
            CancellationToken.None);

        var fakeExportService = new FakeExportService();
        var viewModel = CreateViewModel(repository, projectSetupService, new FakeMediaPlaybackService(), fakeExportService);
        await viewModel.InitializeCommand.ExecuteAsync(null);
        viewModel.SelectedRecentProject = Assert.Single(viewModel.RecentProjects, project => project.Name == "Overlay Match");
        await viewModel.OpenSelectedRecentProjectCommand.ExecuteAsync(null);

        var preset = viewModel.TagPresets.First(candidate => candidate.IconKey == "goal");
        await repository.UpsertTagEventAsync(
            new TagEvent(Guid.NewGuid(), viewModel.SelectedRecentProject!.ProjectId, preset.Id, 100, 130, "Иванов", "P2", null, DateTimeOffset.UtcNow, TeamSide.Home, false),
            CancellationToken.None);

        viewModel.SelectedPreset = preset;
        viewModel.ExportOutputPath = Path.Combine(_tempRootPath, "exports", "overlay.mp4");

        await viewModel.BuildClipsCommand.ExecuteAsync(null);
        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.NotNull(fakeExportService.LastRequest);
        var request = fakeExportService.LastRequest!;
        var segment = Assert.Single(request.Segments);
        Assert.Equal("Гол", segment.Label);
        Assert.Equal("Молот", segment.TeamName);
        Assert.Equal("Иванов", segment.Player);
        Assert.Equal("P2", segment.Period);
        Assert.Equal("00:04", segment.MatchClockText);
        Assert.Equal("#E53935", segment.AccentColorHex);
        Assert.Equal("1/1", segment.CounterText);
    }

    [Fact]
    public async Task OpenSelectedPlaylistCommand_RepairsSingleFramePlaylistItemsFromEventRange()
    {
        var repository = new SqliteProjectRepository(_projectsRootPath);
        var projectSetupService = new ProjectSetupService(repository, _projectsRootPath);

        var result = await projectSetupService.CreateProjectWithVideoAsync(
            new CreateProjectRequestDto("Repair Match", CreateSourceVideoFile("repair.mp4"), "Repair Match"),
            CancellationToken.None);

        var viewModel = CreateViewModel(repository, projectSetupService, new FakeMediaPlaybackService());
        await viewModel.InitializeCommand.ExecuteAsync(null);
        viewModel.SelectedRecentProject = Assert.Single(viewModel.RecentProjects, project => project.Name == "Repair Match");
        await viewModel.OpenSelectedRecentProjectCommand.ExecuteAsync(null);

        var projectId = result.ProjectId;
        var preset = viewModel.TagPresets.First(candidate => candidate.IconKey == "goal");
        var tagEvent = new TagEvent(
            Guid.NewGuid(),
            projectId,
            preset.Id,
            100,
            130,
            "Player A",
            "1",
            null,
            DateTimeOffset.UtcNow,
            TeamSide.Home,
            false);

        await repository.UpsertTagEventAsync(tagEvent, CancellationToken.None);

        var playlist = new Playlist(
            Guid.NewGuid(),
            projectId,
            "Legacy Playlist",
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var legacyItem = new PlaylistItem(
            Guid.NewGuid(),
            playlist.Id,
            tagEvent.Id,
            preset.Id,
            0,
            tagEvent.StartFrame,
            tagEvent.EndFrame,
            tagEvent.StartFrame,
            tagEvent.StartFrame,
            10,
            20,
            preset.Name,
            tagEvent.Player,
            tagEvent.TeamSide);

        await repository.UpsertPlaylistAsync(playlist, CancellationToken.None);
        await repository.ReplacePlaylistItemsAsync(playlist.Id, [legacyItem], CancellationToken.None);

        await viewModel.OpenStartupScreenCommand.ExecuteAsync(null);
        viewModel.SelectedRecentProject = Assert.Single(viewModel.RecentProjects, project => project.Name == "Repair Match");
        await viewModel.OpenSelectedRecentProjectCommand.ExecuteAsync(null);
        viewModel.SelectedPlaylist = Assert.Single(viewModel.Playlists, candidate => candidate.Name == "Legacy Playlist");

        await viewModel.OpenSelectedPlaylistCommand.ExecuteAsync(null);

        var loadedItem = Assert.Single(viewModel.PlaylistItems);
        Assert.Equal(90, loadedItem.ClipStartFrame);
        Assert.Equal(150, loadedItem.ClipEndFrame);
        Assert.Equal("90 → 150", loadedItem.FrameRangeText);
        Assert.Contains("Восстановлены диапазоны", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
        {
            Directory.Delete(_tempRootPath, recursive: true);
        }
    }

    private MainWindowViewModel CreateViewModel(
        SqliteProjectRepository repository,
        ProjectSetupService projectSetupService,
        FakeMediaPlaybackService mediaPlaybackService,
        FakeExportService? exportService = null)
    {
        return new MainWindowViewModel(
            repository,
            projectSetupService,
            new PlaylistService(repository),
            new TagService(),
            new FakeClipComposerService(),
            exportService ?? new FakeExportService(),
            mediaPlaybackService,
            new AppSettingsStore(_settingsPath),
            new AppSettings());
    }

    private string CreateSourceVideoFile(string fileName)
    {
        var sourceFolder = Path.Combine(_tempRootPath, "source");
        Directory.CreateDirectory(sourceFolder);
        var videoPath = Path.Combine(sourceFolder, fileName);
        File.WriteAllText(videoPath, "video");
        return videoPath;
    }

    private sealed class FakeMediaPlaybackService : IMediaPlaybackService
    {
        public event EventHandler? PlaybackStateChanged;
        public event EventHandler<long>? FrameChanged;

        public bool IsPlaying { get; private set; }
        public bool IsMuted { get; private set; }
        public long CurrentFrame { get; private set; }
        public long DurationFrames { get; private set; } = 250;
        public double FramesPerSecond { get; private set; } = 25;
        public int Volume { get; private set; } = 100;
        public double PlaybackRate { get; private set; } = 1.0;

        public Task<MediaMetadata> OpenAsync(string filePath, CancellationToken cancellationToken)
        {
            DurationFrames = 250;
            FramesPerSecond = 25;
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            return Task.FromResult(new MediaMetadata(filePath, FramesPerSecond, DurationFrames, 1920, 1080));
        }

        public void Play()
        {
            IsPlaying = true;
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Pause()
        {
            IsPlaying = false;
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SeekToFrame(long frame)
        {
            CurrentFrame = frame;
            FrameChanged?.Invoke(this, frame);
        }

        public void StepFrameForward()
        {
            SeekToFrame(CurrentFrame + 1);
        }

        public void StepFrameBackward()
        {
            SeekToFrame(Math.Max(0, CurrentFrame - 1));
        }

        public void SetVolume(int volume)
        {
            Volume = volume;
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetPlaybackRate(double playbackRate)
        {
            PlaybackRate = playbackRate;
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleMute()
        {
            IsMuted = !IsMuted;
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeClipComposerService : IClipComposerService
    {
        public IReadOnlyList<ClipSegmentDto> BuildSegments(IEnumerable<TagEvent> events, ClipRecipe recipe, long maxFrame)
        {
            return new FfmpegClipComposerService("ffmpeg").BuildSegments(events, recipe, maxFrame);
        }

        public Task<string> ComposeAsync(
            string sourceVideoPath,
            IReadOnlyList<ClipSegmentDto> segments,
            string outputPath,
            double framesPerSecond,
            string? overlayFilterPath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(outputPath);
        }
    }

    private sealed class FakeExportService : IExportService
    {
        public ExportRequestDto? LastRequest { get; private set; }

        public Task<ExportResultDto> ExportAsync(ExportRequestDto request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new ExportResultDto(true, request.OutputPath, null, null, null));
        }
    }
}


