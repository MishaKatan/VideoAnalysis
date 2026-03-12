using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAnalysis.App.Configuration;
using VideoAnalysis.App.ViewModels.Base;
using VideoAnalysis.App.ViewModels.Items;
using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Core.Enums;
using VideoAnalysis.Core.Models;
using VideoAnalysis.Infrastructure.Media;

namespace VideoAnalysis.App.ViewModels.Shell;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IProjectRepository _repository;
    private readonly ITagService _tagService;
    private readonly IClipComposerService _clipComposerService;
    private readonly IExportService _exportService;
    private readonly IMediaPlaybackService _mediaPlaybackService;
    private readonly AppSettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private Guid _projectId;
    private bool _ignoreFrameChange;
    private IReadOnlyList<ClipSegmentDto> _lastSegments = [];

    public MainWindowViewModel(
        IProjectRepository repository,
        ITagService tagService,
        IClipComposerService clipComposerService,
        IExportService exportService,
        IMediaPlaybackService mediaPlaybackService,
        AppSettingsStore settingsStore,
        AppSettings settings)
    {
        _repository = repository;
        _tagService = tagService;
        _clipComposerService = clipComposerService;
        _exportService = exportService;
        _mediaPlaybackService = mediaPlaybackService;
        _settingsStore = settingsStore;
        _settings = settings;

        ExportOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "video-analysis-export.mp4");
        YandexServiceUrl = _settings.YandexServiceUrl;
        YandexBucket = _settings.YandexBucket;
        YandexAccessKey = _settings.YandexAccessKey;
        YandexSecretKey = _settings.YandexSecretKey;
        YandexRegion = _settings.YandexRegion;
        YandexPrefix = _settings.YandexPrefix;

        _mediaPlaybackService.FrameChanged += OnPlaybackFrameChanged;
        _mediaPlaybackService.PlaybackStateChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            DurationFrames = Math.Max(1, _mediaPlaybackService.DurationFrames);
            FramesPerSecond = _mediaPlaybackService.FramesPerSecond;
            IsPlaying = _mediaPlaybackService.IsPlaying;
            IsMuted = _mediaPlaybackService.IsMuted;
            OnPropertyChanged(nameof(CurrentTimeText));
            OnPropertyChanged(nameof(DurationTimeText));
        });
        RefreshPlaybackUiState();
    }

    public ObservableCollection<TagPreset> TagPresets { get; } = [];
    public ObservableCollection<TagEventItemViewModel> TagEvents { get; } = [];
    public ObservableCollection<AnnotationItemViewModel> Annotations { get; } = [];
    public IReadOnlyList<AnnotationShapeType> ShapeTypes { get; } = Enum.GetValues<AnnotationShapeType>();
    public LibVLCSharp.Shared.MediaPlayer? MediaPlayer => (_mediaPlaybackService as LibVlcMediaPlaybackService)?.MediaPlayer;
    public string CurrentTimeText => FormatTime(CurrentFrame, FramesPerSecond);
    public string DurationTimeText => FormatTime(DurationFrames, FramesPerSecond);
    public string PlaybackButtonText => IsPlaying ? "Pause" : "Play";
    public string PlaybackGlyph => IsPlaying ? "||" : "▶";
    public string VolumeGlyph => IsMuted || Volume == 0 ? "🔇" : "🔊";

    [ObservableProperty] private string _projectName = "Hockey Analysis";
    [ObservableProperty] private string _sourceVideoPath = string.Empty;
    [ObservableProperty] private double _framesPerSecond = 30;
    [ObservableProperty] private long _durationFrames = 1;
    [ObservableProperty] private long _currentFrame;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private int _volume = 100;
    [ObservableProperty] private string _filterPlayer = string.Empty;
    [ObservableProperty] private string _filterPeriod = string.Empty;
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string _tagPlayer = string.Empty;
    [ObservableProperty] private string _tagPeriod = string.Empty;
    [ObservableProperty] private string _tagNotes = string.Empty;
    [ObservableProperty] private long _tagStartFrame;
    [ObservableProperty] private long _tagEndFrame = 1;
    [ObservableProperty] private int _preRollFrames = 30;
    [ObservableProperty] private int _postRollFrames = 30;
    [ObservableProperty] private string _clipSummary = "Segments: 0";
    [ObservableProperty] private TagPreset? _selectedPreset;
    [ObservableProperty] private TagEventItemViewModel? _selectedTagEvent;
    [ObservableProperty] private AnnotationShapeType _selectedShapeType = AnnotationShapeType.Arrow;
    [ObservableProperty] private long _annotationStartFrame;
    [ObservableProperty] private long _annotationEndFrame = 1;
    [ObservableProperty] private double _annotationX1 = 100;
    [ObservableProperty] private double _annotationY1 = 100;
    [ObservableProperty] private double _annotationX2 = 260;
    [ObservableProperty] private double _annotationY2 = 160;
    [ObservableProperty] private string _annotationText = "Play";
    [ObservableProperty] private string _annotationColor = "#FFD700";
    [ObservableProperty] private bool _exportToCloud;
    [ObservableProperty] private string _exportOutputPath;
    [ObservableProperty] private string _yandexServiceUrl = "https://storage.yandexcloud.net";
    [ObservableProperty] private string _yandexBucket = string.Empty;
    [ObservableProperty] private string _yandexAccessKey = string.Empty;
    [ObservableProperty] private string _yandexSecretKey = string.Empty;
    [ObservableProperty] private string _yandexRegion = "ru-central1";
    [ObservableProperty] private string _yandexPrefix = "exports";

    partial void OnCurrentFrameChanged(long value)
    {
        OnPropertyChanged(nameof(CurrentTimeText));
        if (_ignoreFrameChange || DurationFrames <= 0)
        {
            return;
        }

        _mediaPlaybackService.SeekToFrame(value);
    }

    partial void OnFramesPerSecondChanged(double value)
    {
        OnPropertyChanged(nameof(CurrentTimeText));
        OnPropertyChanged(nameof(DurationTimeText));
    }

    partial void OnDurationFramesChanged(long value)
    {
        OnPropertyChanged(nameof(DurationTimeText));
    }

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlaybackButtonText));
        OnPropertyChanged(nameof(PlaybackGlyph));
    }

    partial void OnIsMutedChanged(bool value)
    {
        OnPropertyChanged(nameof(VolumeGlyph));
    }

    partial void OnVolumeChanged(int value)
    {
        _mediaPlaybackService.SetVolume(value);
        OnPropertyChanged(nameof(VolumeGlyph));
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await _repository.InitializeAsync(CancellationToken.None);
        var existingProject = (await _repository.ListProjectsAsync(CancellationToken.None)).FirstOrDefault();
        if (existingProject is null)
        {
            _projectId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var project = new Project(_projectId, ProjectName, now, now, "MVP Hockey project");
            await _repository.CreateProjectAsync(project, CancellationToken.None);
        }
        else
        {
            _projectId = existingProject.Id;
            ProjectName = existingProject.Name;
        }

        var presets = await _repository.GetTagPresetsAsync(_projectId, CancellationToken.None);
        if (presets.Count == 0)
        {
            foreach (var preset in HockeyTagPresets.CreateDefaults(_projectId))
            {
                await _repository.UpsertTagPresetAsync(preset, CancellationToken.None);
            }

            presets = await _repository.GetTagPresetsAsync(_projectId, CancellationToken.None);
        }

        TagPresets.Clear();
        foreach (var preset in presets)
        {
            TagPresets.Add(preset);
        }

        SelectedPreset = TagPresets.FirstOrDefault();
        var mediaAsset = await _repository.GetMediaAssetAsync(_projectId, CancellationToken.None);
        if (mediaAsset is not null)
        {
            SourceVideoPath = mediaAsset.FilePath;
            FramesPerSecond = mediaAsset.FramesPerSecond;
            DurationFrames = mediaAsset.DurationFrames;
            try
            {
                await _mediaPlaybackService.OpenAsync(SourceVideoPath, CancellationToken.None);
                RefreshPlaybackUiState();
            }
            catch
            {
                StatusMessage = "Video file from project is missing or unavailable.";
            }
        }

        await RefreshTagsAsync();
        await RefreshAnnotationsAsync();
    }

    [RelayCommand]
    private async Task ImportFromPathAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceVideoPath))
        {
            StatusMessage = "Select a video file path first.";
            return;
        }

        await ImportVideoAsync(SourceVideoPath);
    }

    public async Task ImportVideoAsync(string path)
    {
        try
        {
            var metadata = await _mediaPlaybackService.OpenAsync(path, CancellationToken.None);
            SourceVideoPath = metadata.FilePath;
            FramesPerSecond = metadata.FramesPerSecond;
            DurationFrames = metadata.DurationFrames;
            CurrentFrame = 0;
            IsPlaying = false;
            RefreshPlaybackUiState();

            var mediaAsset = new MediaAsset(
                Guid.NewGuid(),
                _projectId,
                metadata.FilePath,
                metadata.FramesPerSecond,
                metadata.DurationFrames,
                metadata.Width,
                metadata.Height,
                DateTimeOffset.UtcNow);

            await _repository.UpsertMediaAssetAsync(mediaAsset, CancellationToken.None);
            StatusMessage = "Video imported.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (_mediaPlaybackService.IsPlaying)
        {
            _mediaPlaybackService.Pause();
        }
        else
        {
            _mediaPlaybackService.Play();
        }
    }

    [RelayCommand] private void StepForward() => _mediaPlaybackService.StepFrameForward();
    [RelayCommand] private void StepBackward() => _mediaPlaybackService.StepFrameBackward();

    [RelayCommand]
    private void ToggleMute()
    {
        _mediaPlaybackService.ToggleMute();
        IsMuted = _mediaPlaybackService.IsMuted;
        Volume = _mediaPlaybackService.Volume;
    }

    [RelayCommand]
    private async Task AddPresetAsync()
    {
        var preset = new TagPreset(Guid.NewGuid(), _projectId, $"Custom {TagPresets.Count + 1}", "#FFB300", "Custom", false);
        await _repository.UpsertTagPresetAsync(preset, CancellationToken.None);
        TagPresets.Add(preset);
        SelectedPreset = preset;
        StatusMessage = $"Preset '{preset.Name}' added.";
    }

    [RelayCommand]
    private async Task AddTagAsync()
    {
        if (SelectedPreset is null)
        {
            StatusMessage = "Select a tag preset.";
            return;
        }

        var tagEvent = new TagEvent(
            Guid.NewGuid(),
            _projectId,
            SelectedPreset.Id,
            Math.Max(0, TagStartFrame),
            Math.Max(TagStartFrame, TagEndFrame),
            string.IsNullOrWhiteSpace(TagPlayer) ? null : TagPlayer,
            string.IsNullOrWhiteSpace(TagPeriod) ? null : TagPeriod,
            string.IsNullOrWhiteSpace(TagNotes) ? null : TagNotes,
            DateTimeOffset.UtcNow);

        _tagService.Validate(tagEvent);
        await _repository.UpsertTagEventAsync(tagEvent, CancellationToken.None);
        await RefreshTagsAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedTagAsync()
    {
        if (SelectedTagEvent is null)
        {
            return;
        }

        await _repository.DeleteTagEventAsync(_projectId, SelectedTagEvent.Id, CancellationToken.None);
        await RefreshTagsAsync();
    }

    [RelayCommand]
    private async Task RefreshTagsAsync()
    {
        var query = new TagQuery(null, FilterPlayer, FilterPeriod, FilterText);
        var presetsById = TagPresets.ToDictionary((preset) => preset.Id);
        var events = await _repository.GetTagEventsAsync(_projectId, query, CancellationToken.None);
        var filtered = _tagService.Filter(events, query, presetsById);

        TagEvents.Clear();
        foreach (var tagEvent in filtered)
        {
            if (!presetsById.TryGetValue(tagEvent.TagPresetId, out var preset))
            {
                continue;
            }

            TagEvents.Add(new TagEventItemViewModel
            {
                Id = tagEvent.Id,
                TagPresetId = tagEvent.TagPresetId,
                PresetName = preset.Name,
                StartFrame = tagEvent.StartFrame,
                EndFrame = tagEvent.EndFrame,
                Player = tagEvent.Player ?? string.Empty,
                Period = tagEvent.Period ?? string.Empty,
                Notes = tagEvent.Notes ?? string.Empty
            });
        }

        ClipSummary = $"Segments: {_lastSegments.Count}";
    }

    [RelayCommand]
    private async Task AddAnnotationAsync()
    {
        var annotation = new Annotation(
            Guid.NewGuid(),
            _projectId,
            SelectedTagEvent?.Id,
            Math.Max(0, AnnotationStartFrame),
            Math.Max(AnnotationStartFrame, AnnotationEndFrame),
            SelectedShapeType,
            AnnotationX1,
            AnnotationY1,
            AnnotationX2,
            AnnotationY2,
            string.IsNullOrWhiteSpace(AnnotationText) ? null : AnnotationText,
            string.IsNullOrWhiteSpace(AnnotationColor) ? "#FFFFFF" : AnnotationColor,
            3);

        await _repository.UpsertAnnotationAsync(annotation, CancellationToken.None);
        await RefreshAnnotationsAsync();
    }

    [RelayCommand]
    private async Task BuildClipsAsync()
    {
        var events = await _repository.GetTagEventsAsync(_projectId, new TagQuery(null, FilterPlayer, FilterPeriod, FilterText), CancellationToken.None);
        var recipe = new ClipRecipe(
            Guid.NewGuid(),
            _projectId,
            SelectedPreset?.Name ?? "Clips",
            SelectedPreset?.Id,
            FilterPlayer,
            FilterPeriod,
            FilterText,
            PreRollFrames,
            PostRollFrames,
            DateTimeOffset.UtcNow);

        await _repository.UpsertClipRecipeAsync(recipe, CancellationToken.None);
        _lastSegments = _clipComposerService.BuildSegments(events, recipe, DurationFrames);
        ClipSummary = $"Segments: {_lastSegments.Count}";
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (_lastSegments.Count == 0)
        {
            await BuildClipsAsync();
            if (_lastSegments.Count == 0)
            {
                StatusMessage = "No segments to export.";
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(SourceVideoPath) || !File.Exists(SourceVideoPath))
        {
            StatusMessage = "Source video is missing.";
            return;
        }

        var annotationDtos = Annotations.Select((annotation) => new AnnotationDto(
            annotation.Id,
            annotation.StartFrame,
            annotation.EndFrame,
            annotation.ShapeType,
            annotation.X1,
            annotation.Y1,
            annotation.X2,
            annotation.Y2,
            annotation.Text,
            annotation.ColorHex,
            3)).ToList();

        var request = new ExportRequestDto(
            _projectId,
            SourceVideoPath,
            _lastSegments,
            annotationDtos,
            ExportOutputPath,
            FramesPerSecond,
            ExportToCloud,
            ExportToCloud
                ? new YandexS3Options(YandexServiceUrl, YandexBucket, YandexAccessKey, YandexSecretKey, YandexRegion, YandexPrefix)
                : null);

        var result = await _exportService.ExportAsync(request, CancellationToken.None);
        if (!result.Success)
        {
            StatusMessage = $"Export failed: {result.ErrorMessage}";
            return;
        }

        var job = new ExportJob(
            Guid.NewGuid(),
            _projectId,
            null,
            ExportToCloud ? ExportDestinationType.YandexObjectStorage : ExportDestinationType.Local,
            result.OutputPath,
            result.RemoteObjectKey,
            ExportJobStatus.Succeeded,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        await _repository.UpsertExportJobAsync(job, CancellationToken.None);
        StatusMessage = result.RemoteUrl is null ? $"Exported to {result.OutputPath}" : $"Uploaded: {result.RemoteUrl}";
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsStore.Save(new AppSettings
        {
            FfmpegPath = _settings.FfmpegPath,
            YandexServiceUrl = YandexServiceUrl,
            YandexBucket = YandexBucket,
            YandexAccessKey = YandexAccessKey,
            YandexSecretKey = YandexSecretKey,
            YandexRegion = YandexRegion,
            YandexPrefix = YandexPrefix
        });

        StatusMessage = "Cloud settings saved.";
    }

    private async Task RefreshAnnotationsAsync()
    {
        var annotations = await _repository.GetAnnotationsAsync(_projectId, new FrameRange(0, DurationFrames <= 0 ? long.MaxValue : DurationFrames), CancellationToken.None);
        Annotations.Clear();
        foreach (var annotation in annotations)
        {
            Annotations.Add(new AnnotationItemViewModel
            {
                Id = annotation.Id,
                ShapeType = annotation.ShapeType,
                StartFrame = annotation.StartFrame,
                EndFrame = annotation.EndFrame,
                X1 = annotation.X1,
                Y1 = annotation.Y1,
                X2 = annotation.X2,
                Y2 = annotation.Y2,
                ColorHex = annotation.ColorHex,
                Text = annotation.Text ?? string.Empty
            });
        }
    }

    private void OnPlaybackFrameChanged(object? sender, long frame)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _ignoreFrameChange = true;
            CurrentFrame = frame;
            _ignoreFrameChange = false;
        });
    }

    public void ForceAttachVideoHandle(IntPtr nativeHandle)
    {
        if (_mediaPlaybackService is LibVlcMediaPlaybackService service)
        {
            service.SetVideoOutputHandle(nativeHandle);
        }
    }

    public void RefreshPlaybackUiState()
    {
        Volume = _mediaPlaybackService.Volume;
        IsMuted = _mediaPlaybackService.IsMuted;
        IsPlaying = _mediaPlaybackService.IsPlaying;
        OnPropertyChanged(nameof(CurrentTimeText));
        OnPropertyChanged(nameof(DurationTimeText));
    }

    private static string FormatTime(long frame, double framesPerSecond)
    {
        var fps = framesPerSecond <= 0 ? 30d : framesPerSecond;
        var totalSeconds = Math.Max(0, (int)Math.Floor(frame / fps));
        var time = TimeSpan.FromSeconds(totalSeconds);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }
}
