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
        Assert.Contains("Выберите проект", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
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

        var preset = viewModel.TagPresets.First((candidate) => candidate.Name == "Гол");
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
        Assert.Contains("создан", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
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
        FakeMediaPlaybackService mediaPlaybackService)
    {
        return new MainWindowViewModel(
            repository,
            projectSetupService,
            new PlaylistService(repository),
            new TagService(),
            new FakeClipComposerService(),
            new FakeExportService(),
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
            return [];
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
        public Task<ExportResultDto> ExportAsync(ExportRequestDto request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ExportResultDto(true, request.OutputPath, null, null, null));
        }
    }
}
