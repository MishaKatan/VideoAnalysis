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
    private readonly IProjectSetupService _projectSetupService;
    private readonly IPlaylistService _playlistService;
    private readonly ITagService _tagService;
    private readonly IClipComposerService _clipComposerService;
    private readonly IExportService _exportService;
    private readonly IMediaPlaybackService _mediaPlaybackService;
    private readonly AppSettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private Guid _projectId;
    private bool _ignoreFrameChange;
    private bool _isAdjustingEventTypeHotkey;
    private string _lastValidEventTypeHotkey = string.Empty;
    private readonly HashSet<Guid> _selectedPlaylistTagEventIds = [];
    private IReadOnlyList<ClipSegmentDto> _lastSegments = [];
    private IReadOnlyList<ClipSegmentDto> _activePlaylistSegments = [];
    private int _activePlaylistSegmentIndex = -1;
    private Guid _activePlaylistId;
    private string _projectFolderPath = string.Empty;

    public MainWindowViewModel(
        IProjectRepository repository,
        IProjectSetupService projectSetupService,
        IPlaylistService playlistService,
        ITagService tagService,
        IClipComposerService clipComposerService,
        IExportService exportService,
        IMediaPlaybackService mediaPlaybackService,
        AppSettingsStore settingsStore,
        AppSettings settings)
    {
        _repository = repository;
        _projectSetupService = projectSetupService;
        _playlistService = playlistService;
        _tagService = tagService;
        _clipComposerService = clipComposerService;
        _exportService = exportService;
        _mediaPlaybackService = mediaPlaybackService;
        _settingsStore = settingsStore;
        _settings = settings;

        RecentProjects.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasRecentProjects));
            OnPropertyChanged(nameof(HasNoRecentProjects));
            OnPropertyChanged(nameof(CanOpenSelectedRecentProject));
        };
        Playlists.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPlaylists));
        PlaylistItems.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPlaylistItems));
            OnPropertyChanged(nameof(HasNoPlaylistItems));
        };

        ExportOutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "video-analysis-export.mp4");
        ExportFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Video Analytics", "Exports");
        PlaylistName = "РќРѕРІР°СЏ РїРѕРґР±РѕСЂРєР°";
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
            PlaybackRate = _mediaPlaybackService.PlaybackRate <= 0 ? 1.0 : _mediaPlaybackService.PlaybackRate;
            OnPropertyChanged(nameof(CurrentTimeText));
            OnPropertyChanged(nameof(DurationTimeText));
        });
        RefreshPlaybackUiState();
    }

    public ObservableCollection<TagPreset> TagPresets { get; } = [];
    public ObservableCollection<EventTypeItemViewModel> EventTypeItems { get; } = [];
    public ObservableCollection<TagEventItemViewModel> TagEvents { get; } = [];
    public ObservableCollection<AnnotationItemViewModel> Annotations { get; } = [];
    public ObservableCollection<RecentProjectItemViewModel> RecentProjects { get; } = [];
    public ObservableCollection<PlaylistSummaryItemViewModel> Playlists { get; } = [];
    public ObservableCollection<PlaylistClipItemViewModel> PlaylistItems { get; } = [];
    public ObservableCollection<StatisticsBarItemViewModel> StatisticsItems { get; } = [];
    public IReadOnlyList<AnnotationShapeType> ShapeTypes { get; } = Enum.GetValues<AnnotationShapeType>();
    public IReadOnlyList<TeamSide> EventTeamSides { get; } = [TeamSide.Home, TeamSide.Away];

    public IReadOnlyList<string> PlaybackRateOptions { get; } = ["0.25x", "0.5x", "0.75x", "1.0x", "1.25x", "1.5x", "2.0x"];
    public bool HasRecentProjects => RecentProjects.Count > 0;
    public bool HasNoRecentProjects => RecentProjects.Count == 0;
    public bool HasPlaylistSelection => _selectedPlaylistTagEventIds.Count > 0;
    public bool HasPlaylists => Playlists.Count > 0;
    public bool HasPlaylistItems => PlaylistItems.Count > 0;
    public bool HasNoPlaylistItems => PlaylistItems.Count == 0;
    public bool CanDeleteSelectedPreset => SelectedPreset is not null;
    public bool CanDeleteEditedPreset => IsEditingPreset && SelectedPreset is not null;
    public bool CanDeleteEditedTagEvent => IsEditingTagEvent && SelectedTagEvent is not null;
    public bool CanOpenSelectedRecentProject => SelectedRecentProject is not null;
    public bool CanCloseStartupScreen => _projectId != Guid.Empty;
    public bool CanCreatePlaylist => _projectId != Guid.Empty && _selectedPlaylistTagEventIds.Count > 0;
    public bool CanOpenSelectedPlaylist => SelectedPlaylist is not null;
    public bool CanDeleteSelectedPlaylist => SelectedPlaylist is not null;
    public bool CanPlayActivePlaylist => _activePlaylistSegments.Count > 0;
    public int HomeScore { get; private set; }
    public int AwayScore { get; private set; }
    public string HomeTeamDisplayName { get; private set; } = "РљРѕРјР°РЅРґР° С…РѕР·СЏРµРІ";
    public string AwayTeamDisplayName { get; private set; } = "РљРѕРјР°РЅРґР° РіРѕСЃС‚РµР№";
    public int SelectedPlaylistEventCount => _selectedPlaylistTagEventIds.Count;
    public bool IsEventTypesTabSelected => string.Equals(SelectedEventsPanelTab, "EventTypes", StringComparison.Ordinal);
    public bool IsEventsTabSelected => string.Equals(SelectedEventsPanelTab, "Events", StringComparison.Ordinal);
    public bool IsPlaylistsTabSelected => string.Equals(SelectedRightPanelTab, "Playlists", StringComparison.Ordinal);
    public bool IsStatisticsTabSelected => string.Equals(SelectedRightPanelTab, "Statistics", StringComparison.Ordinal);
    public bool IsPlayerSurfaceVisible => !IsNewProjectDialogOpen && !IsStartupScreenVisible && !IsExportDialogOpen;
    public bool IsTimelineVisible => !IsTimelineHidden;
    public bool IsEventsPanelVisible => !IsEventsPanelHidden;
    public bool IsAnalysisPanelVisible => !IsAnalysisPanelHidden;
    public bool IsStartupScreenVisible => IsStartupScreenOpen && !IsNewProjectDialogOpen;
    public string PresetEditorTitle => IsEditingPreset ? "Р РµРґР°РєС‚РёСЂРѕРІР°РЅРёРµ С‚РёРїР° СЃРѕР±С‹С‚РёСЏ" : "РќРѕРІС‹Р№ С‚РёРї СЃРѕР±С‹С‚РёСЏ";
    public string TagEventEditorTitle => IsEditingTagEvent ? "Р РµРґР°РєС‚РёСЂРѕРІР°РЅРёРµ СЃРѕР±С‹С‚РёСЏ" : "РќРѕРІРѕРµ СЃРѕР±С‹С‚РёРµ";
    public LibVLCSharp.Shared.MediaPlayer? MediaPlayer => (_mediaPlaybackService as LibVlcMediaPlaybackService)?.MediaPlayer;
    public string CurrentTimeText => FormatTime(CurrentFrame, FramesPerSecond);
    public string DurationTimeText => FormatTime(DurationFrames, FramesPerSecond);
    public string PlaybackButtonText => IsPlaying ? "Pause" : "Play";
    public string PlaybackGlyph => IsPlaying ? "||" : "в–¶";
    public bool ShowOverlayPlayButton => !IsPlaying;
    public string PlaybackRateText => $"{PlaybackRate:0.##}x";
    public string VolumeGlyph => IsMuted || Volume == 0 ? "🔇" : "🔊";
    public bool IsExportAllClipsSelected => SelectedExportSource == ExportSourceOption.AllClips;
    public bool IsExportPlaylistSelected => SelectedExportSource == ExportSourceOption.Playlist;
    public bool IsExportFullMatchSelected => SelectedExportSource == ExportSourceOption.FullMatch;
    public bool IsExportFormatMp4Selected => SelectedExportFormat == ExportFormatOption.Mp4;
    public bool IsExportFormatAviSelected => SelectedExportFormat == ExportFormatOption.Avi;
    public bool IsExportFormatMovSelected => SelectedExportFormat == ExportFormatOption.Mov;
    public bool IsExportQualityLowSelected => SelectedExportQuality == ExportQualityOption.Low720p;
    public bool IsExportQualityMediumSelected => SelectedExportQuality == ExportQualityOption.Medium1080p;
    public bool IsExportQualityHighSelected => SelectedExportQuality == ExportQualityOption.High4K;
    public bool IsExportDestinationFolderSelected => SelectedExportDestination == ExportDestinationOption.Folder;
    public bool IsExportDestinationTelegramSelected => SelectedExportDestination == ExportDestinationOption.Telegram;
    public bool IsExportDestinationBothSelected => SelectedExportDestination == ExportDestinationOption.Both;
    public bool CanExportFromDialog => _projectId != Guid.Empty && !string.IsNullOrWhiteSpace(SourceVideoPath) && !string.IsNullOrWhiteSpace(ExportFolderPath) && !IsExportInProgress;
    public bool CanCloseExportDialog => !IsExportInProgress;
    public string ExportPrimaryButtonText => IsExportInProgress ? "Рендерим..." : "Экспортировать";

    [ObservableProperty] private string _projectName = "Hockey Analysis";
    [ObservableProperty] private string _sourceVideoPath = string.Empty;
    [ObservableProperty] private double _framesPerSecond = 30;
    [ObservableProperty] private long _durationFrames = 1;
    [ObservableProperty] private long _currentFrame;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private int _volume = 100;

    [ObservableProperty] private double _playbackRate = 1.0;
    [ObservableProperty] private string _filterPlayer = string.Empty;
    [ObservableProperty] private string _filterPeriod = string.Empty;
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string _tagPlayer = string.Empty;
    [ObservableProperty] private string _tagPeriod = string.Empty;
    [ObservableProperty] private string _tagNotes = string.Empty;
    [ObservableProperty] private TeamSide _tagTeamSide = TeamSide.Home;
    [ObservableProperty] private long _tagStartFrame;
    [ObservableProperty] private long _tagEndFrame = 1;
    [ObservableProperty] private int _preRollFrames = 30;
    [ObservableProperty] private int _postRollFrames = 30;
    [ObservableProperty] private string _clipSummary = "Segments: 0";
    [ObservableProperty] private string _playlistName = string.Empty;
    [ObservableProperty] private string _playlistDescription = string.Empty;
    [ObservableProperty] private string _selectedEventsPanelTab = "EventTypes";
    [ObservableProperty] private string _selectedRightPanelTab = "Playlists";
    [ObservableProperty] private bool _isTimelineHidden;
    [ObservableProperty] private bool _isEventsPanelHidden;
    [ObservableProperty] private bool _isAnalysisPanelHidden;
    [ObservableProperty] private bool _isPresetEditorOpen;
    [ObservableProperty] private bool _isEditingPreset;
    [ObservableProperty] private bool _isTagEventEditorOpen;
    [ObservableProperty] private bool _isEditingTagEvent;
    [ObservableProperty] private bool _isPlaylistPlaybackActive;
    [ObservableProperty] private bool _isStartupScreenOpen = true;
    [ObservableProperty] private bool _isNewProjectDialogOpen;
    [ObservableProperty] private bool _isExportDialogOpen;
    [ObservableProperty] private RecentProjectItemViewModel? _selectedRecentProject;
    [ObservableProperty] private PlaylistSummaryItemViewModel? _selectedPlaylist;
    [ObservableProperty] private PlaylistClipItemViewModel? _selectedPlaylistItem;
    [ObservableProperty] private TagPreset? _selectedPreset;
    [ObservableProperty] private EventTypeItemViewModel? _selectedEventTypeItem;
    [ObservableProperty] private string _newProjectName = string.Empty;
    [ObservableProperty] private string _newProjectDescription = string.Empty;
    [ObservableProperty] private string _newProjectHomeTeam = string.Empty;
    [ObservableProperty] private string _newProjectAwayTeam = string.Empty;
    [ObservableProperty] private string _newProjectVideoPath = string.Empty;
    [ObservableProperty] private string _eventTypeName = string.Empty;
    [ObservableProperty] private string _eventTypeHotkey = string.Empty;
    [ObservableProperty] private string _eventTypeColor = "#FFB300";
    [ObservableProperty] private string _eventTypeCategory = "Custom";
    [ObservableProperty] private string _eventTypeIconKey = "event";
    [ObservableProperty] private bool _eventTypeShowInStatistics = true;
    [ObservableProperty] private int _eventTypePreRollFrames;
    [ObservableProperty] private int _eventTypePostRollFrames;
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
    [ObservableProperty] private string _exportFolderPath = string.Empty;
    [ObservableProperty] private bool _exportIncludeTacticalDrawings;
    [ObservableProperty] private bool _isExportInProgress;
    [ObservableProperty] private string _exportProgressText = "Подготовка к экспорту...";
    [ObservableProperty] private ExportSourceOption _selectedExportSource = ExportSourceOption.AllClips;
    [ObservableProperty] private ExportFormatOption _selectedExportFormat = ExportFormatOption.Mp4;
    [ObservableProperty] private ExportQualityOption _selectedExportQuality = ExportQualityOption.High4K;
    [ObservableProperty] private ExportDestinationOption _selectedExportDestination = ExportDestinationOption.Folder;
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

    partial void OnSourceVideoPathChanged(string value)
    {
        OnPropertyChanged(nameof(CanExportFromDialog));
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
        OnPropertyChanged(nameof(ShowOverlayPlayButton));
    }

    partial void OnIsMutedChanged(bool value)
    {
        OnPropertyChanged(nameof(VolumeGlyph));
    }

    partial void OnPlaybackRateChanged(double value)
    {
        var normalizedRate = Math.Clamp(value, 0.25d, 2.0d);
        if (Math.Abs(normalizedRate - value) > 0.0001d)
        {
            PlaybackRate = normalizedRate;
            return;
        }

        _mediaPlaybackService.SetPlaybackRate(normalizedRate);
        OnPropertyChanged(nameof(PlaybackRateText));
    }

    partial void OnSelectedPresetChanged(TagPreset? value)
    {
        OnPropertyChanged(nameof(CanDeleteSelectedPreset));
        OnPropertyChanged(nameof(CanDeleteEditedPreset));

        if (value is null)
        {
            if (SelectedEventTypeItem is not null)
            {
                SelectedEventTypeItem = null;
            }
            return;
        }

        if (SelectedEventTypeItem?.Id != value.Id)
        {
            SelectedEventTypeItem = EventTypeItems.FirstOrDefault((item) => item.Id == value.Id);
        }

        EventTypeName = value.Name;
        EventTypeHotkey = value.Hotkey;
        EventTypeColor = value.ColorHex;
        EventTypeCategory = value.Category;
        EventTypeIconKey = value.IconKey;
        EventTypeShowInStatistics = value.ShowInStatistics;
        EventTypePreRollFrames = Math.Max(0, value.PreRollFrames);
        EventTypePostRollFrames = Math.Max(0, value.PostRollFrames);
    }

    partial void OnSelectedEventTypeItemChanged(EventTypeItemViewModel? value)
    {
        if (value is null || SelectedPreset?.Id == value.Id)
        {
            return;
        }

        SelectedPreset = value.Preset;
    }

    partial void OnEventTypeHotkeyChanged(string value)
    {
        if (_isAdjustingEventTypeHotkey)
        {
            return;
        }

        var normalizedHotkey = NormalizeSingleEnglishHotkey(value);
        var nextHotkey = normalizedHotkey ?? _lastValidEventTypeHotkey;

        if (normalizedHotkey is not null && HasHotkeyConflict(normalizedHotkey))
        {
            nextHotkey = _lastValidEventTypeHotkey;
            StatusMessage = $"Hotkey '{normalizedHotkey}' is already assigned to another event type.";
        }

        if (!string.Equals(value, nextHotkey, StringComparison.Ordinal))
        {
            _isAdjustingEventTypeHotkey = true;
            EventTypeHotkey = nextHotkey;
            _isAdjustingEventTypeHotkey = false;
            return;
        }

        _lastValidEventTypeHotkey = nextHotkey;
    }

    partial void OnSelectedTagEventChanged(TagEventItemViewModel? value)
    {
        OnPropertyChanged(nameof(CanDeleteEditedTagEvent));
    }

    partial void OnSelectedRecentProjectChanged(RecentProjectItemViewModel? value)
    {
        OnPropertyChanged(nameof(CanOpenSelectedRecentProject));
    }

    partial void OnSelectedPlaylistChanged(PlaylistSummaryItemViewModel? value)
    {
        OnPropertyChanged(nameof(CanOpenSelectedPlaylist));
        OnPropertyChanged(nameof(CanDeleteSelectedPlaylist));
    }

    partial void OnSelectedEventsPanelTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsEventTypesTabSelected));
        OnPropertyChanged(nameof(IsEventsTabSelected));
    }

    partial void OnSelectedRightPanelTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsPlaylistsTabSelected));
        OnPropertyChanged(nameof(IsStatisticsTabSelected));
    }

    partial void OnIsTimelineHiddenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsTimelineVisible));
    }

    partial void OnIsEventsPanelHiddenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEventsPanelVisible));
    }

    partial void OnIsAnalysisPanelHiddenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAnalysisPanelVisible));
    }

    partial void OnIsEditingPresetChanged(bool value)
    {
        OnPropertyChanged(nameof(PresetEditorTitle));
        OnPropertyChanged(nameof(CanDeleteEditedPreset));
    }

    partial void OnIsEditingTagEventChanged(bool value)
    {
        OnPropertyChanged(nameof(TagEventEditorTitle));
        OnPropertyChanged(nameof(CanDeleteEditedTagEvent));
    }

    partial void OnIsNewProjectDialogOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPlayerSurfaceVisible));
        OnPropertyChanged(nameof(IsStartupScreenVisible));
    }

    partial void OnIsExportDialogOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPlayerSurfaceVisible));
    }

    partial void OnIsStartupScreenOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStartupScreenVisible));
        OnPropertyChanged(nameof(IsPlayerSurfaceVisible));
        OnPropertyChanged(nameof(CanCloseStartupScreen));
    }

    partial void OnVolumeChanged(int value)
    {
        _mediaPlaybackService.SetVolume(value);
        OnPropertyChanged(nameof(VolumeGlyph));
    }

    partial void OnSelectedExportSourceChanged(ExportSourceOption value)
    {
        OnPropertyChanged(nameof(IsExportAllClipsSelected));
        OnPropertyChanged(nameof(IsExportPlaylistSelected));
        OnPropertyChanged(nameof(IsExportFullMatchSelected));
        UpdateExportOutputPath();
    }

    partial void OnSelectedExportFormatChanged(ExportFormatOption value)
    {
        OnPropertyChanged(nameof(IsExportFormatMp4Selected));
        OnPropertyChanged(nameof(IsExportFormatAviSelected));
        OnPropertyChanged(nameof(IsExportFormatMovSelected));
        UpdateExportOutputPath();
    }

    partial void OnSelectedExportQualityChanged(ExportQualityOption value)
    {
        OnPropertyChanged(nameof(IsExportQualityLowSelected));
        OnPropertyChanged(nameof(IsExportQualityMediumSelected));
        OnPropertyChanged(nameof(IsExportQualityHighSelected));
    }

    partial void OnSelectedExportDestinationChanged(ExportDestinationOption value)
    {
        OnPropertyChanged(nameof(IsExportDestinationFolderSelected));
        OnPropertyChanged(nameof(IsExportDestinationTelegramSelected));
        OnPropertyChanged(nameof(IsExportDestinationBothSelected));
    }

    partial void OnExportFolderPathChanged(string value)
    {
        OnPropertyChanged(nameof(CanExportFromDialog));
        UpdateExportOutputPath();
    }

    partial void OnIsExportInProgressChanged(bool value)
    {
        OnPropertyChanged(nameof(CanExportFromDialog));
        OnPropertyChanged(nameof(CanCloseExportDialog));
        OnPropertyChanged(nameof(ExportPrimaryButtonText));
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await _repository.InitializeAsync(CancellationToken.None);
        await RefreshRecentProjectsAsync(CancellationToken.None);
        ResetCurrentProjectState();

        IsStartupScreenOpen = true;
        StatusMessage = HasRecentProjects
            ? "Выберите проект для продолжения."
            : "Создайте проект, чтобы начать работу.";
    }

    [RelayCommand]
    private async Task ImportFromPathAsync()
    {
        if (_projectId == Guid.Empty)
        {
            StatusMessage = "РЎРЅР°С‡Р°Р»Р° СЃРѕР·РґР°Р№С‚Рµ РїСЂРѕРµРєС‚.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SourceVideoPath))
        {
            StatusMessage = "Select a video file path first.";
            return;
        }

        await ImportVideoAsync(SourceVideoPath);
    }

    public async Task ImportVideoAsync(string path)
    {
        if (_projectId == Guid.Empty)
        {
            StatusMessage = "РЎРЅР°С‡Р°Р»Р° СЃРѕР·РґР°Р№С‚Рµ РїСЂРѕРµРєС‚.";
            return;
        }

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
    private void SeekBackwardFiveSeconds()
    {
        var offsetFrames = (long)Math.Round(Math.Max(1d, FramesPerSecond) * 5d);
        CurrentFrame = Math.Max(0, CurrentFrame - offsetFrames);
    }

    [RelayCommand]
    private void SeekForwardFiveSeconds()
    {
        var offsetFrames = (long)Math.Round(Math.Max(1d, FramesPerSecond) * 5d);
        CurrentFrame = Math.Min(DurationFrames, CurrentFrame + offsetFrames);
    }

    [RelayCommand]
    private void SetPlaybackRate(string? playbackRateText)
    {
        if (string.IsNullOrWhiteSpace(playbackRateText))
        {
            return;
        }

        var normalizedText = playbackRateText.Trim().Replace("x", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!double.TryParse(normalizedText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var playbackRate))
        {
            return;
        }

        PlaybackRate = playbackRate;
    }

    [RelayCommand]
    private void ToggleMute()
    {
        var nextMutedState = !IsMuted;
        _mediaPlaybackService.ToggleMute();
        IsMuted = nextMutedState;
        Volume = _mediaPlaybackService.Volume;
        OnPropertyChanged(nameof(VolumeGlyph));
    }

    [RelayCommand]
    private void SelectEventsPanelTab(string tabKey)
    {
        SelectedEventsPanelTab = string.IsNullOrWhiteSpace(tabKey) ? "EventTypes" : tabKey;
    }

    [RelayCommand]
    private void SelectRightPanelTab(string tabKey)
    {
        SelectedRightPanelTab = string.IsNullOrWhiteSpace(tabKey) ? "Playlists" : tabKey;
    }

    [RelayCommand]
    private void OpenExportDialog()
    {
        OpenExportDialogCore(_activePlaylistSegments.Count > 0 ? ExportSourceOption.Playlist : ExportSourceOption.AllClips);
    }

    [RelayCommand]
    private void OpenExportDialogForPlaylist()
    {
        OpenExportDialogCore(ExportSourceOption.Playlist);
    }

    [RelayCommand]
    private void CloseExportDialog()
    {
        if (IsExportInProgress)
        {
            return;
        }

        IsExportDialogOpen = false;
    }

    [RelayCommand]
    private void SelectExportSource(string? value)
    {
        if (Enum.TryParse<ExportSourceOption>(value, true, out var parsed))
        {
            SelectedExportSource = parsed;
        }
    }

    [RelayCommand]
    private void SelectExportFormat(string? value)
    {
        if (Enum.TryParse<ExportFormatOption>(value, true, out var parsed))
        {
            SelectedExportFormat = parsed;
        }
    }

    [RelayCommand]
    private void SelectExportQuality(string? value)
    {
        if (Enum.TryParse<ExportQualityOption>(value, true, out var parsed))
        {
            SelectedExportQuality = parsed;
        }
    }

    [RelayCommand]
    private void SelectExportDestination(string? value)
    {
        if (Enum.TryParse<ExportDestinationOption>(value, true, out var parsed))
        {
            SelectedExportDestination = parsed;
        }
    }

    [RelayCommand]
    private void ToggleTimelineVisibility()
    {
        IsTimelineHidden = !IsTimelineHidden;
    }

    [RelayCommand]
    private void ToggleEventsPanelVisibility()
    {
        IsEventsPanelHidden = !IsEventsPanelHidden;
    }

    [RelayCommand]
    private void ToggleAnalysisPanelVisibility()
    {
        IsAnalysisPanelHidden = !IsAnalysisPanelHidden;
    }

    public async Task HandleEventTypeHotkeyAsync(string hotkey)
    {
        if (_projectId == Guid.Empty || string.IsNullOrWhiteSpace(hotkey))
        {
            return;
        }

        var normalizedHotkey = hotkey.Trim().ToUpperInvariant();
        var preset = TagPresets.FirstOrDefault((candidate) =>
            string.Equals(candidate.Hotkey?.Trim(), normalizedHotkey, StringComparison.OrdinalIgnoreCase));

        if (preset is null)
        {
            return;
        }

        SelectedEventsPanelTab = "Events";

        if (IsTagEventEditorOpen)
        {
            var matchesSelectedPreset =
                SelectedPreset is not null &&
                string.Equals(SelectedPreset.Hotkey?.Trim(), normalizedHotkey, StringComparison.OrdinalIgnoreCase);

            if (matchesSelectedPreset)
            {
                TagEndFrame = Math.Max(TagStartFrame, CurrentFrame);
                await AddTagAsync();
                return;
            }

            SelectedPreset = preset;
            StatusMessage = $"Event type switched to '{preset.Name}'.";
            return;
        }

        SelectedPreset = preset;
        SelectedTagEvent = null;
        IsEditingTagEvent = false;
        TagStartFrame = Math.Max(0, CurrentFrame - Math.Max(0, preset.PreRollFrames));
        TagEndFrame = CurrentFrame;
        TagPlayer = string.Empty;
        TagPeriod = string.Empty;
        TagNotes = string.Empty;
        if (TagTeamSide is TeamSide.Unknown or TeamSide.Neutral)
        {
            TagTeamSide = TeamSide.Home;
        }

        IsTagEventEditorOpen = true;
        StatusMessage = $"New '{preset.Name}' event started.";
    }

    private void ResetPresetEditorFields()
    {
        if (IsEditingPreset && SelectedPreset is not null)
        {
            EventTypeName = SelectedPreset.Name;
            EventTypeHotkey = SelectedPreset.Hotkey;
            EventTypeColor = SelectedPreset.ColorHex;
            EventTypeCategory = SelectedPreset.Category;
            EventTypeIconKey = SelectedPreset.IconKey;
            EventTypeShowInStatistics = SelectedPreset.ShowInStatistics;
            EventTypePreRollFrames = Math.Max(0, SelectedPreset.PreRollFrames);
            EventTypePostRollFrames = Math.Max(0, SelectedPreset.PostRollFrames);
            return;
        }

        EventTypeName = string.Empty;
        EventTypeHotkey = string.Empty;
        EventTypeColor = "#FFB300";
        EventTypeCategory = "Custom";
        EventTypeIconKey = "event";
        EventTypeShowInStatistics = true;
        EventTypePreRollFrames = 0;
        EventTypePostRollFrames = 0;
    }
    [RelayCommand]
    private void OpenNewPresetEditor()
    {
        IsEditingPreset = false;
        SelectedPreset = null;
        ResetPresetEditorFields();
        IsPresetEditorOpen = true;
    }

    [RelayCommand]
    private void ClosePresetEditor()
    {
        ResetPresetEditorFields();
        IsPresetEditorOpen = false;
    }

    [RelayCommand]
    private void OpenNewTagEventEditor()
    {
        IsEditingTagEvent = false;
        SelectedTagEvent = null;
        if (SelectedPreset is null)
        {
            SelectedPreset = TagPresets.FirstOrDefault();
        }

        var preRollFrames = Math.Max(0, SelectedPreset?.PreRollFrames ?? 0);
        TagStartFrame = Math.Max(0, CurrentFrame - preRollFrames);
        TagEndFrame = CurrentFrame;
        TagTeamSide = TeamSide.Home;
        TagPlayer = string.Empty;
        TagPeriod = string.Empty;
        TagNotes = string.Empty;
        IsTagEventEditorOpen = true;
    }

    [RelayCommand]
    private void CloseTagEventEditor()
    {
        IsTagEventEditorOpen = false;
    }

    [RelayCommand]
    private async Task OpenStartupScreenAsync()
    {
        await RefreshRecentProjectsAsync(CancellationToken.None);
        IsStartupScreenOpen = true;
        StatusMessage = HasRecentProjects
            ? "Выберите проект для продолжения."
            : "Создайте проект, чтобы начать работу.";
    }

    [RelayCommand]
    private void CloseStartupScreen()
    {
        if (_projectId == Guid.Empty)
        {
            return;
        }

        IsStartupScreenOpen = false;
    }

    [RelayCommand]
    private async Task OpenSelectedRecentProjectAsync()
    {
        if (SelectedRecentProject is null && RecentProjects.Count > 0)
        {
            SelectedRecentProject = RecentProjects[0];
        }

        if (SelectedRecentProject is null)
        {
            StatusMessage = HasRecentProjects
                ? "РЎРЅР°С‡Р°Р»Р° РІС‹Р±РµСЂРёС‚Рµ РїСЂРѕРµРєС‚."
                : "РџСЂРѕРµРєС‚РѕРІ РїРѕРєР° РЅРµС‚.";
            return;
        }

        await OpenRecentProjectAsync(SelectedRecentProject);
    }

    [RelayCommand]
    private async Task OpenRecentProjectAsync(RecentProjectItemViewModel? recentProject)
    {
        if (recentProject is null)
        {
            return;
        }

        var project = await _repository.GetProjectAsync(recentProject.ProjectId, CancellationToken.None);
        if (project is null)
        {
            StatusMessage = "The selected project could not be found.";
            await RefreshRecentProjectsAsync(CancellationToken.None);
            return;
        }

        await LoadProjectAsync(project, CancellationToken.None);
        IsStartupScreenOpen = false;
        StatusMessage = $"Project '{project.Name}' opened.";
    }

    [RelayCommand]
    private void OpenNewProjectDialog()
    {
        NewProjectName = string.Empty;
        NewProjectDescription = string.Empty;
        NewProjectHomeTeam = string.Empty;
        NewProjectAwayTeam = string.Empty;
        NewProjectVideoPath = string.Empty;
        IsNewProjectDialogOpen = true;
    }

    [RelayCommand]
    private void CloseNewProjectDialog()
    {
        IsNewProjectDialogOpen = false;
    }

    [RelayCommand]
    private async Task ContinueNewProjectLegacyAsync()
    {
        StatusMessage = "РџРµСЂРµС…РѕРґ Рє РёРјРїРѕСЂС‚Сѓ РІРёРґРµРѕ РїРѕРєР° РЅРµ СЂРµР°Р»РёР·РѕРІР°РЅ.";
        IsNewProjectDialogOpen = false;
    }

    [RelayCommand]
    private async Task ContinueNewProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName))
        {
            StatusMessage = "Project name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewProjectVideoPath))
        {
            StatusMessage = "Select a video file.";
            return;
        }

        try
        {
            var result = await _projectSetupService.CreateProjectWithVideoAsync(
                new CreateProjectRequestDto(
                    NewProjectName.Trim(),
                    NewProjectVideoPath.Trim(),
                    Description: string.IsNullOrWhiteSpace(NewProjectDescription) ? null : NewProjectDescription.Trim(),
                    HomeTeamName: string.IsNullOrWhiteSpace(NewProjectHomeTeam) ? null : NewProjectHomeTeam.Trim(),
                    AwayTeamName: string.IsNullOrWhiteSpace(NewProjectAwayTeam) ? null : NewProjectAwayTeam.Trim(),
                    MoveVideoToProjectFolder: false),
                CancellationToken.None);

            var project = await _repository.GetProjectAsync(result.ProjectId, CancellationToken.None)
                ?? throw new InvalidOperationException("Created project could not be loaded.");

            await LoadProjectAsync(project, CancellationToken.None);
            await RefreshRecentProjectsAsync(CancellationToken.None);
            SelectedRecentProject = RecentProjects.FirstOrDefault((item) => item.ProjectId == project.Id);
            IsStartupScreenOpen = false;
            IsNewProjectDialogOpen = false;
            StatusMessage = $"Project '{project.Name}' created.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Project creation failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddPresetAsync()
    {
        var preset = new TagPreset(
            Guid.NewGuid(),
            _projectId,
            string.IsNullOrWhiteSpace(EventTypeName) ? $"Custom {TagPresets.Count + 1}" : EventTypeName.Trim(),
            string.IsNullOrWhiteSpace(EventTypeColor) ? "#FFB300" : EventTypeColor.Trim(),
            string.IsNullOrWhiteSpace(EventTypeCategory) ? "Custom" : EventTypeCategory.Trim(),
            false,
            string.IsNullOrWhiteSpace(EventTypeHotkey) ? string.Empty : EventTypeHotkey.Trim(),
            string.IsNullOrWhiteSpace(EventTypeIconKey) ? "event" : EventTypeIconKey.Trim(),
            EventTypeShowInStatistics,
            Math.Max(0, EventTypePreRollFrames),
            Math.Max(0, EventTypePostRollFrames));

        await _repository.UpsertTagPresetAsync(preset, CancellationToken.None);
        TagPresets.Add(preset);
        SelectedPreset = preset;
        IsEditingPreset = true;
        IsPresetEditorOpen = false;
        await RefreshTagsAsync();
        StatusMessage = $"Preset '{preset.Name}' added.";
    }

    [RelayCommand]
    private async Task SavePresetAsync()
    {
        if (!IsEditingPreset)
        {
            await AddPresetAsync();
            return;
        }

        if (SelectedPreset is null)
        {
            StatusMessage = "Select an event type first.";
            return;
        }

        var updatedPreset = SelectedPreset with
        {
            Name = string.IsNullOrWhiteSpace(EventTypeName) ? SelectedPreset.Name : EventTypeName.Trim(),
            Hotkey = string.IsNullOrWhiteSpace(EventTypeHotkey) ? string.Empty : EventTypeHotkey.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(EventTypeColor) ? SelectedPreset.ColorHex : EventTypeColor.Trim(),
            Category = string.IsNullOrWhiteSpace(EventTypeCategory) ? "Custom" : EventTypeCategory.Trim(),
            IconKey = string.IsNullOrWhiteSpace(EventTypeIconKey) ? "event" : EventTypeIconKey.Trim(),
            ShowInStatistics = EventTypeShowInStatistics,
            PreRollFrames = Math.Max(0, EventTypePreRollFrames),
            PostRollFrames = Math.Max(0, EventTypePostRollFrames)
        };

        await _repository.UpsertTagPresetAsync(updatedPreset, CancellationToken.None);

        var selectedIndex = TagPresets.IndexOf(SelectedPreset);
        if (selectedIndex >= 0)
        {
            TagPresets[selectedIndex] = updatedPreset;
        }

        SelectedPreset = updatedPreset;
        IsPresetEditorOpen = false;
        await RefreshTagsAsync();
        StatusMessage = $"Preset '{updatedPreset.Name}' updated.";
    }

    [RelayCommand]
    private async Task DeletePresetAsync()
    {
        if (SelectedPreset is null)
        {
            StatusMessage = "Select an event type first.";
            return;
        }

        var presetToDelete = SelectedPreset;
        await _repository.DeleteTagPresetAsync(_projectId, presetToDelete.Id, CancellationToken.None);
        TagPresets.Remove(presetToDelete);
        SelectedPreset = TagPresets.FirstOrDefault();
        IsPresetEditorOpen = false;
        IsEditingPreset = false;
        await RefreshTagsAsync();
        StatusMessage = $"Preset '{presetToDelete.Name}' deleted.";
    }

    [RelayCommand]
    private async Task AddTagAsync()
    {
        if (SelectedPreset is null)
        {
            StatusMessage = "Select a tag preset.";
            return;
        }

        var eventId = IsEditingTagEvent && SelectedTagEvent is not null
            ? SelectedTagEvent.Id
            : Guid.NewGuid();

        var effectiveEndFrame = Math.Max(
            TagStartFrame,
            TagEndFrame + (!IsEditingTagEvent ? Math.Max(0, SelectedPreset.PostRollFrames) : 0));

        var tagEvent = new TagEvent(
            eventId,
            _projectId,
            SelectedPreset.Id,
            Math.Max(0, TagStartFrame),
            effectiveEndFrame,
            string.IsNullOrWhiteSpace(TagPlayer) ? null : TagPlayer,
            string.IsNullOrWhiteSpace(TagPeriod) ? null : TagPeriod,
            string.IsNullOrWhiteSpace(TagNotes) ? null : TagNotes,
            DateTimeOffset.UtcNow,
            TagTeamSide);

        _tagService.Validate(tagEvent);
        await _repository.UpsertTagEventAsync(tagEvent, CancellationToken.None);
        await RefreshTagsAsync();
        IsTagEventEditorOpen = false;
        IsEditingTagEvent = true;
        StatusMessage = $"Event '{SelectedPreset.Name}' saved.";
    }

    [RelayCommand]
    private void UseCurrentFrameForTagStart() => TagStartFrame = CurrentFrame;

    [RelayCommand]
    private void UseCurrentFrameForTagEnd() => TagEndFrame = CurrentFrame;

    public void SeekToTagEventStart(TagEventItemViewModel tagEvent)
    {
        SelectedTagEvent = tagEvent;
        CurrentFrame = Math.Max(0, tagEvent.StartFrame);
        StatusMessage = $"Jumped to event '{tagEvent.PresetName}'.";
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
        IsTagEventEditorOpen = false;
        IsEditingTagEvent = false;
        StatusMessage = "Event deleted.";
    }

    private static TeamSide NormalizeEventTeamSide(TeamSide teamSide)
    {
        return teamSide is TeamSide.Away ? TeamSide.Away : TeamSide.Home;
    }

    private async Task<IReadOnlyList<TagEvent>> NormalizeEventTeamSidesAsync(IReadOnlyList<TagEvent> events)
    {
        if (_projectId == Guid.Empty || events.Count == 0)
        {
            return events;
        }

        var normalizedEvents = new List<TagEvent>(events.Count);
        foreach (var sourceTagEvent in events)
        {
            var tagEvent = sourceTagEvent;
            var normalizedTeamSide = NormalizeEventTeamSide(tagEvent.TeamSide);
            if (normalizedTeamSide != tagEvent.TeamSide)
            {
                tagEvent = tagEvent with { TeamSide = normalizedTeamSide };
                await _repository.UpsertTagEventAsync(tagEvent, CancellationToken.None);
            }

            normalizedEvents.Add(tagEvent);
        }

        return normalizedEvents;
    }

    private async Task RefreshTagsAsync()
    {
        var query = new TagQuery(null, FilterPlayer, FilterPeriod, FilterText);
        var presetsById = TagPresets.ToDictionary((preset) => preset.Id);
        var events = await _repository.GetTagEventsAsync(_projectId, query, CancellationToken.None);
        events = await NormalizeEventTeamSidesAsync(events);
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
                TeamSide = tagEvent.TeamSide.ToString(),
                StartFrame = tagEvent.StartFrame,
                EndFrame = tagEvent.EndFrame,
                Player = tagEvent.Player ?? string.Empty,
                Period = tagEvent.Period ?? string.Empty,
                Notes = tagEvent.Notes ?? string.Empty,
                IsSelectedForPlaylist = _selectedPlaylistTagEventIds.Contains(tagEvent.Id)
            });
        }

        var allEvents = await _repository.GetTagEventsAsync(_projectId, new TagQuery(null, null, null, null), CancellationToken.None);
        allEvents = await NormalizeEventTeamSidesAsync(allEvents);
        RefreshEventTypeItems(allEvents);
        RefreshStatistics(allEvents);

        ClipSummary = $"Segments: {_lastSegments.Count}";
        OnPropertyChanged(nameof(HasPlaylistSelection));
        OnPropertyChanged(nameof(CanCreatePlaylist));
        OnPropertyChanged(nameof(SelectedPlaylistEventCount));
    }

    [RelayCommand]
    private void TogglePlaylistSelection(TagEventItemViewModel? tagEvent)
    {
        if (tagEvent is null)
        {
            return;
        }

        if (_selectedPlaylistTagEventIds.Contains(tagEvent.Id))
        {
            _selectedPlaylistTagEventIds.Remove(tagEvent.Id);
            tagEvent.IsSelectedForPlaylist = false;
        }
        else
        {
            _selectedPlaylistTagEventIds.Add(tagEvent.Id);
            tagEvent.IsSelectedForPlaylist = true;
        }

        StatusMessage = _selectedPlaylistTagEventIds.Count == 0
            ? "РџРѕРґР±РѕСЂРєР° РѕС‡РёС‰РµРЅР°."
            : $"Р’С‹Р±СЂР°РЅРѕ СЃРѕР±С‹С‚РёР№ РґР»СЏ РїРѕРґР±РѕСЂРєРё: {_selectedPlaylistTagEventIds.Count}.";
        OnPropertyChanged(nameof(HasPlaylistSelection));
        OnPropertyChanged(nameof(CanCreatePlaylist));
        OnPropertyChanged(nameof(SelectedPlaylistEventCount));
    }

    [RelayCommand]
    private async Task CreatePlaylistAsync()
    {
        if (_projectId == Guid.Empty)
        {
            StatusMessage = "РЎРЅР°С‡Р°Р»Р° РѕС‚РєСЂРѕР№С‚Рµ РїСЂРѕРµРєС‚.";
            return;
        }

        if (_selectedPlaylistTagEventIds.Count == 0)
        {
            StatusMessage = "РЎРЅР°С‡Р°Р»Р° РІС‹Р±РµСЂРёС‚Рµ СЃРѕР±С‹С‚РёСЏ РґР»СЏ РїРѕРґР±РѕСЂРєРё.";
            return;
        }

        var request = new CreatePlaylistRequestDto(
            _projectId,
            string.IsNullOrWhiteSpace(PlaylistName) ? $"РџРѕРґР±РѕСЂРєР° {DateTime.Now:dd.MM HH:mm}" : PlaylistName.Trim(),
            _selectedPlaylistTagEventIds.ToList(),
            Math.Max(0, PreRollFrames),
            Math.Max(0, PostRollFrames),
            string.IsNullOrWhiteSpace(PlaylistDescription) ? null : PlaylistDescription.Trim(),
            DurationFrames > 0 ? DurationFrames : null);

        try
        {
            var playlist = await _playlistService.CreatePlaylistAsync(request, CancellationToken.None);
            await RefreshPlaylistsAsync(CancellationToken.None);
            ApplyLoadedPlaylist(playlist);
            _selectedPlaylistTagEventIds.Clear();
            foreach (var tagEvent in TagEvents)
            {
                tagEvent.IsSelectedForPlaylist = false;
            }

            OnPropertyChanged(nameof(HasPlaylistSelection));
            OnPropertyChanged(nameof(CanCreatePlaylist));
            OnPropertyChanged(nameof(SelectedPlaylistEventCount));
            StatusMessage = $"Плейлист '{playlist.Name}' создан.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕР·РґР°С‚СЊ РїР»РµР№Р»РёСЃС‚: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenSelectedPlaylistAsync()
    {
        if (SelectedPlaylist is null)
        {
            StatusMessage = "Р’С‹Р±РµСЂРёС‚Рµ РїР»РµР№Р»РёСЃС‚.";
            return;
        }

        var playlist = await _playlistService.GetPlaylistAsync(_projectId, SelectedPlaylist.Id, CancellationToken.None);
        if (playlist is null)
        {
            StatusMessage = "РџР»РµР№Р»РёСЃС‚ РЅРµ РЅР°Р№РґРµРЅ.";
            await RefreshPlaylistsAsync(CancellationToken.None);
            return;
        }

        ApplyLoadedPlaylist(playlist);
        StatusMessage = $"РџР»РµР№Р»РёСЃС‚ '{playlist.Name}' РѕС‚РєСЂС‹С‚.";
    }

    [RelayCommand]
    private async Task DeleteSelectedPlaylistAsync()
    {
        if (SelectedPlaylist is null)
        {
            StatusMessage = "Р’С‹Р±РµСЂРёС‚Рµ РїР»РµР№Р»РёСЃС‚.";
            return;
        }

        var playlistToDelete = SelectedPlaylist;
        await _playlistService.DeletePlaylistAsync(_projectId, playlistToDelete.Id, CancellationToken.None);

        if (_activePlaylistId == playlistToDelete.Id)
        {
            _activePlaylistId = Guid.Empty;
            _activePlaylistSegments = [];
            _lastSegments = [];
            _activePlaylistSegmentIndex = -1;
            IsPlaylistPlaybackActive = false;
            PlaylistItems.Clear();
            SelectedPlaylistItem = null;
            ClipSummary = "Segments: 0";
        HomeScore = 0;
        AwayScore = 0;
        OnPropertyChanged(nameof(HomeScore));
        OnPropertyChanged(nameof(AwayScore));
        OnPropertyChanged(nameof(HomeTeamDisplayName));
        OnPropertyChanged(nameof(AwayTeamDisplayName));
            OnPropertyChanged(nameof(CanPlayActivePlaylist));
        }

        await RefreshPlaylistsAsync(CancellationToken.None);
        StatusMessage = $"РџР»РµР№Р»РёСЃС‚ '{playlistToDelete.Name}' СѓРґР°Р»С‘РЅ.";
    }

    [RelayCommand]
    private void SeekToPlaylistItem(PlaylistClipItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedPlaylistItem = item;
        CurrentFrame = Math.Max(0, item.ClipStartFrame);
        StatusMessage = $"РџРµСЂРµС…РѕРґ Рє РєР»РёРїСѓ '{item.Label}'.";
    }

    [RelayCommand]
    private void PlayActivePlaylist()
    {
        if (_activePlaylistSegments.Count == 0)
        {
            StatusMessage = "РЎРЅР°С‡Р°Р»Р° РѕС‚РєСЂРѕР№С‚Рµ РёР»Рё СЃРѕР·РґР°Р№С‚Рµ РїР»РµР№Р»РёСЃС‚.";
            return;
        }

        _activePlaylistSegmentIndex = 0;
        StartPlaylistSegment(_activePlaylistSegmentIndex);
    }

    [RelayCommand]
    private void StopPlaylistPlayback()
    {
        if (!IsPlaylistPlaybackActive && _activePlaylistSegments.Count == 0)
        {
            return;
        }

        _mediaPlaybackService.Pause();
        IsPlaylistPlaybackActive = false;
        _activePlaylistSegmentIndex = -1;
        StatusMessage = "Р’РѕСЃРїСЂРѕРёР·РІРµРґРµРЅРёРµ РїР»РµР№Р»РёСЃС‚Р° РѕСЃС‚Р°РЅРѕРІР»РµРЅРѕ.";
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
            StatusMessage = $"Export error: {result.ErrorMessage}";
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
    private async Task ExportFromDialogAsync()
    {
        if (IsExportInProgress)
        {
            return;
        }

        if (_projectId == Guid.Empty)
        {
            StatusMessage = "Сначала откройте проект.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SourceVideoPath) || !File.Exists(SourceVideoPath))
        {
            StatusMessage = "Исходное видео не найдено.";
            return;
        }

        if (SelectedExportDestination == ExportDestinationOption.Telegram)
        {
            StatusMessage = "Экспорт в Telegram пока не реализован. Пока доступно сохранение в папку.";
            return;
        }

        IReadOnlyList<ClipSegmentDto> segments;
        try
        {
            segments = await ResolveExportSegmentsAsync();
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return;
        }

        try
        {
            IsExportInProgress = true;
            ExportProgressText = "Подготавливаем экспорт...";
            StatusMessage = "Подготавливаем экспорт...";
            await Task.Yield();

            var exportFolder = GetResolvedExportFolderPath();
            Directory.CreateDirectory(exportFolder);
            ExportOutputPath = Path.Combine(exportFolder, BuildExportFileName() + GetExportFileExtension());

            var annotationDtos = ExportIncludeTacticalDrawings
                ? Annotations.Select((annotation) => new AnnotationDto(
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
                    3)).ToList()
                : [];

            var request = new ExportRequestDto(
                _projectId,
                SourceVideoPath,
                segments,
                annotationDtos,
                ExportOutputPath,
                FramesPerSecond,
                false,
                null);

            ExportProgressText = "Рендерим видео. Это может занять некоторое время...";
            StatusMessage = "Рендерим видео...";

            var result = await _exportService.ExportAsync(request, CancellationToken.None);
            if (!result.Success)
            {
                StatusMessage = $"Export error: {result.ErrorMessage}";
                return;
            }

            ExportProgressText = "Сохраняем результат...";

            var job = new ExportJob(
                Guid.NewGuid(),
                _projectId,
                _activePlaylistId == Guid.Empty ? null : _activePlaylistId,
                ExportDestinationType.Local,
                result.OutputPath,
                null,
                ExportJobStatus.Succeeded,
                null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
            await _repository.UpsertExportJobAsync(job, CancellationToken.None);

            IsExportDialogOpen = false;
            StatusMessage = SelectedExportDestination == ExportDestinationOption.Both
                ? $"Экспорт сохранён в папку. Отправка в Telegram пока не реализована: {result.OutputPath}"
                : $"Экспорт сохранён: {result.OutputPath}";
        }
        finally
        {
            IsExportInProgress = false;
            ExportProgressText = "Подготовка к экспорту...";
        }
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

        StatusMessage = "РќР°СЃС‚СЂРѕР№РєРё РѕР±Р»Р°РєР° СЃРѕС…СЂР°РЅРµРЅС‹.";
    }

    private async Task RefreshRecentProjectsAsync(CancellationToken cancellationToken)
    {
        var projects = await _repository.ListProjectsAsync(cancellationToken);
        var recentProjects = projects
            .OrderByDescending((project) => project.UpdatedAtUtc)
            .Take(3)
            .ToList();

        var recentItems = new List<RecentProjectItemViewModel>(recentProjects.Count);
        foreach (var project in recentProjects)
        {
            var projectVideo = await _repository.GetProjectVideoAsync(project.Id, cancellationToken);
            recentItems.Add(new RecentProjectItemViewModel
            {
                ProjectId = project.Id,
                Name = project.Name,
                Matchup = FormatProjectMatchup(project),
                Summary = FormatProjectSummary(project, projectVideo),
                UpdatedAtText = $"РћР±РЅРѕРІР»РµРЅ {project.UpdatedAtUtc.ToLocalTime():dd.MM.yyyy}"
            });
        }

        RecentProjects.Clear();
        foreach (var item in recentItems)
        {
            RecentProjects.Add(item);
        }

        SelectedRecentProject = RecentProjects.FirstOrDefault((item) => item.ProjectId == _projectId)
            ?? RecentProjects.FirstOrDefault();
    }

    private void ResetCurrentProjectState()
    {
        _projectId = Guid.Empty;
        _projectFolderPath = string.Empty;
        ProjectName = "Hockey Analysis";
        HomeTeamDisplayName = "РљРѕРјР°РЅРґР° С…РѕР·СЏРµРІ";
        AwayTeamDisplayName = "РљРѕРјР°РЅРґР° РіРѕСЃС‚РµР№";
        TagPresets.Clear();
        TagEvents.Clear();
        Annotations.Clear();
        Playlists.Clear();
        PlaylistItems.Clear();
        StatisticsItems.Clear();
        _selectedPlaylistTagEventIds.Clear();
        _activePlaylistSegments = [];
        _activePlaylistSegmentIndex = -1;
        _activePlaylistId = Guid.Empty;
        _lastSegments = [];
        SelectedPreset = null;
        SelectedTagEvent = null;
        SelectedPlaylist = null;
        SelectedPlaylistItem = null;
        SourceVideoPath = string.Empty;
        CurrentFrame = 0;
        DurationFrames = 1;
        FramesPerSecond = 30;
        IsPlaying = false;
        IsPlaylistPlaybackActive = false;
        IsExportDialogOpen = false;
        PlaylistName = "РќРѕРІР°СЏ РїРѕРґР±РѕСЂРєР°";
        PlaylistDescription = string.Empty;
        ClipSummary = "Segments: 0";
        ExportFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Video Analytics", "Exports");
        HomeScore = 0;
        AwayScore = 0;
        OnPropertyChanged(nameof(HomeScore));
        OnPropertyChanged(nameof(AwayScore));
        OnPropertyChanged(nameof(HasPlaylistSelection));
        OnPropertyChanged(nameof(CanCreatePlaylist));
        OnPropertyChanged(nameof(CanPlayActivePlaylist));
        OnPropertyChanged(nameof(SelectedPlaylistEventCount));
        OnPropertyChanged(nameof(CanCloseStartupScreen));
    }

    private static string FormatProjectMatchup(Project project)
    {
        var hasHome = !string.IsNullOrWhiteSpace(project.HomeTeamName);
        var hasAway = !string.IsNullOrWhiteSpace(project.AwayTeamName);

        if (hasHome && hasAway)
        {
            return $"{project.HomeTeamName} - {project.AwayTeamName}";
        }

        if (hasHome)
        {
            return $"{project.HomeTeamName} - TBD";
        }

        if (hasAway)
        {
            return $"TBD - {project.AwayTeamName}";
        }

        return "РљРѕРјР°РЅРґС‹ РµС‰Рµ РЅРµ СѓРєР°Р·Р°РЅС‹";
    }

    private static string FormatProjectSummary(Project project, ProjectVideo? projectVideo)
    {
        if (!string.IsNullOrWhiteSpace(project.Description))
        {
            return project.Description!;
        }

        if (projectVideo is not null)
        {
            return $"Р’РёРґРµРѕ: {projectVideo.Title}";
        }

        return "РџСЂРѕРµРєС‚ РіРѕС‚РѕРІ Рє СЂР°Р·Р±РѕСЂСѓ.";
    }

    private async Task LoadProjectAsync(Project project, CancellationToken cancellationToken)
    {
        _selectedPlaylistTagEventIds.Clear();
        _activePlaylistSegments = [];
        _activePlaylistSegmentIndex = -1;
        _activePlaylistId = Guid.Empty;
        _lastSegments = [];
        IsPlaylistPlaybackActive = false;
        IsExportDialogOpen = false;
        Playlists.Clear();
        PlaylistItems.Clear();
        StatisticsItems.Clear();
        SelectedPlaylist = null;
        SelectedPlaylistItem = null;
        ClipSummary = "Segments: 0";
        HomeScore = 0;
        AwayScore = 0;
        OnPropertyChanged(nameof(HomeScore));
        OnPropertyChanged(nameof(AwayScore));
        _projectId = project.Id;
        _projectFolderPath = project.ProjectFolderPath;
        ProjectName = project.Name;
        HomeTeamDisplayName = string.IsNullOrWhiteSpace(project.HomeTeamName) ? "РљРѕРјР°РЅРґР° С…РѕР·СЏРµРІ" : project.HomeTeamName;
        AwayTeamDisplayName = string.IsNullOrWhiteSpace(project.AwayTeamName) ? "РљРѕРјР°РЅРґР° РіРѕСЃС‚РµР№" : project.AwayTeamName;
        OnPropertyChanged(nameof(HomeTeamDisplayName));
        OnPropertyChanged(nameof(AwayTeamDisplayName));
        PlaylistName = $"{project.Name} playlist";
        PlaylistDescription = string.Empty;
        ExportFolderPath = Path.Combine(_projectFolderPath, "exports");
        UpdateExportOutputPath();
        OnPropertyChanged(nameof(HasPlaylistSelection));
        OnPropertyChanged(nameof(CanCreatePlaylist));
        OnPropertyChanged(nameof(SelectedPlaylistEventCount));
        OnPropertyChanged(nameof(CanPlayActivePlaylist));
        OnPropertyChanged(nameof(CanCloseStartupScreen));

        await EnsureDefaultPresetsAsync(cancellationToken);
        await LoadProjectVideoAsync(cancellationToken);
        await RefreshTagsAsync();
        await RefreshAnnotationsAsync();
        await RefreshPlaylistsAsync(cancellationToken);
    }

    private async Task EnsureDefaultPresetsAsync(CancellationToken cancellationToken)
    {
        var presets = await _repository.GetTagPresetsAsync(_projectId, cancellationToken);
        if (presets.Count == 0)
        {
            foreach (var preset in HockeyTagPresets.CreateDefaults(_projectId))
            {
                await _repository.UpsertTagPresetAsync(preset, cancellationToken);
            }

            presets = await _repository.GetTagPresetsAsync(_projectId, cancellationToken);
        }

        TagPresets.Clear();
        foreach (var preset in presets)
        {
            TagPresets.Add(preset);
        }

        RefreshEventTypeItems();
        SelectedPreset = TagPresets.FirstOrDefault();
    }

    private void RefreshEventTypeItems(IReadOnlyList<TagEvent>? allEvents = null)
    {
        var countsByPresetId = (allEvents ?? [])
            .GroupBy((tagEvent) => tagEvent.TagPresetId)
            .ToDictionary((group) => group.Key, (group) => group.Count());
        var selectedPresetId = SelectedPreset?.Id ?? SelectedEventTypeItem?.Id;

        EventTypeItems.Clear();
        foreach (var preset in TagPresets)
        {
            EventTypeItems.Add(new EventTypeItemViewModel
            {
                Preset = preset,
                EventCount = countsByPresetId.TryGetValue(preset.Id, out var count) ? count : 0
            });
        }

        if (selectedPresetId is not null)
        {
            var selectedItem = EventTypeItems.FirstOrDefault((item) => item.Id == selectedPresetId.Value);
            if (selectedItem is not null)
            {
                SelectedEventTypeItem = selectedItem;
                SelectedPreset = selectedItem.Preset;
            }
        }
    }

    private void RefreshStatistics(IReadOnlyList<TagEvent> allEvents)
    {
        var events = allEvents ?? [];
        var scoreEvents = events.Where((tagEvent) =>
            TagPresets.Any((preset) => preset.Id == tagEvent.TagPresetId &&
                (string.Equals(preset.Name, "Goal", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(preset.Name, "Р“РѕР»", StringComparison.OrdinalIgnoreCase))));

        HomeScore = scoreEvents.Count((tagEvent) => NormalizeEventTeamSide(tagEvent.TeamSide) == TeamSide.Home);
        AwayScore = scoreEvents.Count((tagEvent) => NormalizeEventTeamSide(tagEvent.TeamSide) == TeamSide.Away);
        OnPropertyChanged(nameof(HomeScore));
        OnPropertyChanged(nameof(AwayScore));

        var countsByPresetId = events
            .GroupBy((tagEvent) => tagEvent.TagPresetId)
            .ToDictionary(
                (group) => group.Key,
                (group) => new
                {
                    Home = group.Count((tagEvent) => NormalizeEventTeamSide(tagEvent.TeamSide) == TeamSide.Home),
                    Away = group.Count((tagEvent) => NormalizeEventTeamSide(tagEvent.TeamSide) == TeamSide.Away)
                });

        StatisticsItems.Clear();
        foreach (var preset in TagPresets.Where((preset) => preset.ShowInStatistics))
        {
            var counts = countsByPresetId.TryGetValue(preset.Id, out var value) ? value : new { Home = 0, Away = 0 };
            StatisticsItems.Add(new StatisticsBarItemViewModel
            {
                Name = preset.Name,
                HomeCount = counts.Home,
                AwayCount = counts.Away
            });
        }
    }

    private async Task LoadProjectVideoAsync(CancellationToken cancellationToken)
    {
        var projectVideo = await _repository.GetProjectVideoAsync(_projectId, cancellationToken);
        if (projectVideo is null)
        {
            SourceVideoPath = string.Empty;
            FramesPerSecond = 30;
            DurationFrames = 1;
            CurrentFrame = 0;
            return;
        }

        SourceVideoPath = projectVideo.StoredFilePath;
        try
        {
            var metadata = await _mediaPlaybackService.OpenAsync(SourceVideoPath, cancellationToken);
            FramesPerSecond = metadata.FramesPerSecond;
            DurationFrames = Math.Max(1, metadata.DurationFrames);
            CurrentFrame = 0;
            IsPlaying = false;
            RefreshPlaybackUiState();
        }
        catch
        {
            StatusMessage = "Video file from project is missing or unavailable.";
        }
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

    private async Task RefreshPlaylistsAsync(CancellationToken cancellationToken)
    {
        var playlists = await _playlistService.GetPlaylistsAsync(_projectId, cancellationToken);

        Playlists.Clear();
        foreach (var playlist in playlists)
        {
            Playlists.Add(new PlaylistSummaryItemViewModel
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Description = string.IsNullOrWhiteSpace(playlist.Description) ? "Р‘РµР· РѕРїРёСЃР°РЅРёСЏ" : playlist.Description,
                ItemCount = playlist.ItemCount,
                UpdatedAtText = $"{playlist.ItemCount} РєР»РёРїРѕРІ вЂў {playlist.UpdatedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}"
            });
        }

        SelectedPlaylist = Playlists.FirstOrDefault((item) => item.Id == SelectedPlaylist?.Id) ?? Playlists.FirstOrDefault();
    }

    private void ApplyLoadedPlaylist(PlaylistDetailsDto playlist)
    {
        _activePlaylistId = playlist.Id;
        _activePlaylistSegments = playlist.Items
            .OrderBy((item) => item.SortOrder)
            .Select((item) => new ClipSegmentDto(item.TagEventId, item.ClipStartFrame, item.ClipEndFrame, item.Label, item.Player))
            .ToList();

        _lastSegments = _activePlaylistSegments;
        _activePlaylistSegmentIndex = -1;
        IsPlaylistPlaybackActive = false;
        PlaylistName = playlist.Name;
        PlaylistDescription = playlist.Description ?? string.Empty;
        ClipSummary = $"Segments: {_lastSegments.Count}";

        PlaylistItems.Clear();
        foreach (var item in playlist.Items.OrderBy((playlistItem) => playlistItem.SortOrder))
        {
            PlaylistItems.Add(new PlaylistClipItemViewModel
            {
                Id = item.Id,
                TagEventId = item.TagEventId,
                Label = item.Label,
                Player = string.IsNullOrWhiteSpace(item.Player) ? "Р‘РµР· РёРіСЂРѕРєР°" : item.Player,
                TeamSide = item.TeamSide.ToString(),
                ClipStartFrame = item.ClipStartFrame,
                ClipEndFrame = item.ClipEndFrame,
                FrameRangeText = $"{item.ClipStartFrame} в†’ {item.ClipEndFrame}"
            });
        }

        SelectedPlaylist = Playlists.FirstOrDefault((candidate) => candidate.Id == playlist.Id) ?? SelectedPlaylist;
        SelectedPlaylistItem = PlaylistItems.FirstOrDefault();
        OnPropertyChanged(nameof(CanPlayActivePlaylist));
    }

    private void OpenExportDialogCore(ExportSourceOption defaultSource)
    {
        if (_projectId == Guid.Empty)
        {
            StatusMessage = "Сначала откройте проект.";
            return;
        }

        SelectedExportSource = defaultSource == ExportSourceOption.Playlist && _activePlaylistSegments.Count == 0
            ? ExportSourceOption.AllClips
            : defaultSource;
        ExportFolderPath = GetResolvedExportFolderPath();
        UpdateExportOutputPath();
        IsExportDialogOpen = true;
    }

    private async Task<IReadOnlyList<ClipSegmentDto>> ResolveExportSegmentsAsync()
    {
        switch (SelectedExportSource)
        {
            case ExportSourceOption.Playlist:
                if (_activePlaylistSegments.Count == 0)
                {
                    throw new InvalidOperationException("Откройте плейлист перед экспортом.");
                }

                return _activePlaylistSegments;

            case ExportSourceOption.FullMatch:
                if (DurationFrames <= 1)
                {
                    throw new InvalidOperationException("Для проекта еще не загружено видео.");
                }

                return
                [
                    new ClipSegmentDto(Guid.Empty, 0, Math.Max(0, DurationFrames - 1), ProjectName, null)
                ];

            default:
                await BuildClipsAsync();
                if (_lastSegments.Count == 0)
                {
                    throw new InvalidOperationException("Нет клипов для экспорта по текущим фильтрам.");
                }

                return _lastSegments;
        }
    }

    private string GetResolvedExportFolderPath()
    {
        if (!string.IsNullOrWhiteSpace(ExportFolderPath))
        {
            return ExportFolderPath.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_projectFolderPath))
        {
            return Path.Combine(_projectFolderPath, "exports");
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Video Analytics", "Exports");
    }

    private void UpdateExportOutputPath()
    {
        var folderPath = GetResolvedExportFolderPath();
        ExportOutputPath = Path.Combine(folderPath, BuildExportFileName() + GetExportFileExtension());
    }

    private string BuildExportFileName()
    {
        var rawName = SelectedExportSource switch
        {
            ExportSourceOption.Playlist when !string.IsNullOrWhiteSpace(PlaylistName) => PlaylistName.Trim(),
            ExportSourceOption.Playlist when SelectedPlaylist is not null => SelectedPlaylist.Name,
            ExportSourceOption.FullMatch => $"{ProjectName} full match",
            _ when SelectedPreset is not null => $"{ProjectName} {SelectedPreset.Name}",
            _ => $"{ProjectName} clips"
        };

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(rawName.Select((character) => invalidChars.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "video-analysis-export" : sanitized.Trim();
    }

    private string GetExportFileExtension()
    {
        return SelectedExportFormat switch
        {
            ExportFormatOption.Avi => ".avi",
            ExportFormatOption.Mov => ".mov",
            _ => ".mp4"
        };
    }

    private void StartPlaylistSegment(int index)
    {
        if (index < 0 || index >= _activePlaylistSegments.Count)
        {
            StopPlaylistPlayback();
            return;
        }

        var segment = _activePlaylistSegments[index];
        _activePlaylistSegmentIndex = index;
        SelectedPlaylistItem = index < PlaylistItems.Count ? PlaylistItems[index] : null;
        _mediaPlaybackService.SeekToFrame(segment.StartFrame);
        _mediaPlaybackService.Play();
        IsPlaylistPlaybackActive = true;
        StatusMessage = $"РџР»РµР№Р»РёСЃС‚: РєР»РёРї {index + 1}/{_activePlaylistSegments.Count} '{segment.Label}'.";
    }

    private void AdvancePlaylistPlayback(long currentFrame)
    {
        if (!IsPlaylistPlaybackActive || _activePlaylistSegmentIndex < 0 || _activePlaylistSegmentIndex >= _activePlaylistSegments.Count)
        {
            return;
        }

        var currentSegment = _activePlaylistSegments[_activePlaylistSegmentIndex];
        if (currentFrame <= currentSegment.EndFrame)
        {
            return;
        }

        var nextIndex = _activePlaylistSegmentIndex + 1;
        if (nextIndex >= _activePlaylistSegments.Count)
        {
            _mediaPlaybackService.Pause();
            IsPlaylistPlaybackActive = false;
            _activePlaylistSegmentIndex = -1;
            StatusMessage = "РџР»РµР№Р»РёСЃС‚ РІРѕСЃРїСЂРѕРёР·РІРµРґРµРЅ РїРѕР»РЅРѕСЃС‚СЊСЋ.";
            return;
        }

        StartPlaylistSegment(nextIndex);
    }

    private void OnPlaybackFrameChanged(object? sender, long frame)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _ignoreFrameChange = true;
            CurrentFrame = frame;
            _ignoreFrameChange = false;
            AdvancePlaylistPlayback(frame);
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
        PlaybackRate = _mediaPlaybackService.PlaybackRate <= 0 ? 1.0 : _mediaPlaybackService.PlaybackRate;
        IsPlaying = _mediaPlaybackService.IsPlaying;
        OnPropertyChanged(nameof(CurrentTimeText));
        OnPropertyChanged(nameof(DurationTimeText));
    }

    public void OpenPresetEditor(TagPreset preset)
    {
        SelectedPreset = preset;
        IsEditingPreset = true;
        ResetPresetEditorFields();
        IsPresetEditorOpen = true;
    }

    public void OpenTagEventEditor(TagEventItemViewModel tagEvent)
    {
        SelectedTagEvent = tagEvent;
        SelectedPreset = TagPresets.FirstOrDefault((preset) => preset.Id == tagEvent.TagPresetId) ?? SelectedPreset;
        TagStartFrame = tagEvent.StartFrame;
        TagEndFrame = tagEvent.EndFrame;
        TagPlayer = tagEvent.Player;
        TagPeriod = tagEvent.Period;
        TagNotes = tagEvent.Notes;
        TagTeamSide = Enum.TryParse<TeamSide>(tagEvent.TeamSide, out var parsedTeamSide)
            ? NormalizeEventTeamSide(parsedTeamSide)
            : TeamSide.Home;
        IsEditingTagEvent = true;
        IsTagEventEditorOpen = true;
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

    private bool HasHotkeyConflict(string candidateHotkey)
    {
        if (string.IsNullOrEmpty(candidateHotkey))
        {
            return false;
        }

        var editedPresetId = SelectedPreset?.Id;
        return TagPresets.Any((preset) =>
            preset.Id != editedPresetId &&
            string.Equals(preset.Hotkey?.Trim(), candidateHotkey, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeSingleEnglishHotkey(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        for (var index = value.Length - 1; index >= 0; index--)
        {
            var character = value[index];
            if (character is >= 'A' and <= 'Z')
            {
                return character.ToString();
            }

            if (character is >= 'a' and <= 'z')
            {
                return char.ToUpperInvariant(character).ToString();
            }
        }

        return null;
    }
}

public enum ExportSourceOption
{
    AllClips,
    Playlist,
    FullMatch
}

public enum ExportFormatOption
{
    Mp4,
    Avi,
    Mov
}

public enum ExportQualityOption
{
    Low720p,
    Medium1080p,
    High4K
}

public enum ExportDestinationOption
{
    Folder,
    Telegram,
    Both
}

























