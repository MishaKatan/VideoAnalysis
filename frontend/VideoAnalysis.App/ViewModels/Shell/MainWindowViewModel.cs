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
        PlaylistName = "Новая подборка";
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
    public ObservableCollection<RecentProjectItemViewModel> RecentProjects { get; } = [];
    public ObservableCollection<PlaylistSummaryItemViewModel> Playlists { get; } = [];
    public ObservableCollection<PlaylistClipItemViewModel> PlaylistItems { get; } = [];
    public IReadOnlyList<AnnotationShapeType> ShapeTypes { get; } = Enum.GetValues<AnnotationShapeType>();
    public IReadOnlyList<TeamSide> EventTeamSides { get; } = [TeamSide.Home, TeamSide.Away, TeamSide.Neutral];
    public bool HasRecentProjects => RecentProjects.Count > 0;
    public bool HasNoRecentProjects => RecentProjects.Count == 0;
    public bool HasPlaylistSelection => _selectedPlaylistTagEventIds.Count > 0;
    public bool HasPlaylists => Playlists.Count > 0;
    public bool HasPlaylistItems => PlaylistItems.Count > 0;
    public bool HasNoPlaylistItems => PlaylistItems.Count == 0;
    public bool CanDeleteSelectedPreset => SelectedPreset is { IsSystem: false };
    public bool CanDeleteEditedPreset => IsEditingPreset && SelectedPreset is { IsSystem: false };
    public bool CanDeleteEditedTagEvent => IsEditingTagEvent && SelectedTagEvent is not null;
    public bool CanOpenSelectedRecentProject => SelectedRecentProject is not null;
    public bool CanCloseStartupScreen => _projectId != Guid.Empty;
    public bool CanCreatePlaylist => _projectId != Guid.Empty && _selectedPlaylistTagEventIds.Count > 0;
    public bool CanOpenSelectedPlaylist => SelectedPlaylist is not null;
    public bool CanPlayActivePlaylist => _activePlaylistSegments.Count > 0;
    public int SelectedPlaylistEventCount => _selectedPlaylistTagEventIds.Count;
    public bool IsEventTypesTabSelected => string.Equals(SelectedEventsPanelTab, "EventTypes", StringComparison.Ordinal);
    public bool IsEventsTabSelected => string.Equals(SelectedEventsPanelTab, "Events", StringComparison.Ordinal);
    public bool IsPlayerSurfaceVisible => !IsNewProjectDialogOpen && !IsStartupScreenVisible;
    public bool IsStartupScreenVisible => IsStartupScreenOpen && !IsNewProjectDialogOpen;
    public string PresetEditorTitle => IsEditingPreset ? "Редактирование типа события" : "Новый тип события";
    public string TagEventEditorTitle => IsEditingTagEvent ? "Редактирование события" : "Новое событие";
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
    [ObservableProperty] private TeamSide _tagTeamSide = TeamSide.Neutral;
    [ObservableProperty] private long _tagStartFrame;
    [ObservableProperty] private long _tagEndFrame = 1;
    [ObservableProperty] private int _preRollFrames = 30;
    [ObservableProperty] private int _postRollFrames = 30;
    [ObservableProperty] private string _clipSummary = "Segments: 0";
    [ObservableProperty] private string _playlistName = string.Empty;
    [ObservableProperty] private string _playlistDescription = string.Empty;
    [ObservableProperty] private string _selectedEventsPanelTab = "EventTypes";
    [ObservableProperty] private bool _isPresetEditorOpen;
    [ObservableProperty] private bool _isEditingPreset;
    [ObservableProperty] private bool _isTagEventEditorOpen;
    [ObservableProperty] private bool _isEditingTagEvent;
    [ObservableProperty] private bool _isPlaylistPlaybackActive;
    [ObservableProperty] private bool _isStartupScreenOpen = true;
    [ObservableProperty] private bool _isNewProjectDialogOpen;
    [ObservableProperty] private RecentProjectItemViewModel? _selectedRecentProject;
    [ObservableProperty] private PlaylistSummaryItemViewModel? _selectedPlaylist;
    [ObservableProperty] private PlaylistClipItemViewModel? _selectedPlaylistItem;
    [ObservableProperty] private TagPreset? _selectedPreset;
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

    partial void OnSelectedPresetChanged(TagPreset? value)
    {
        OnPropertyChanged(nameof(CanDeleteSelectedPreset));
        OnPropertyChanged(nameof(CanDeleteEditedPreset));
        if (value is null)
        {
            return;
        }

        EventTypeName = value.Name;
        EventTypeHotkey = value.Hotkey;
        EventTypeColor = value.ColorHex;
        EventTypeCategory = value.Category;
        EventTypeIconKey = value.IconKey;
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
    }

    partial void OnSelectedEventsPanelTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsEventTypesTabSelected));
        OnPropertyChanged(nameof(IsEventsTabSelected));
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
            StatusMessage = "Сначала создайте проект.";
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
            StatusMessage = "Сначала создайте проект.";
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
    private void ToggleMute()
    {
        _mediaPlaybackService.ToggleMute();
        IsMuted = _mediaPlaybackService.IsMuted;
        Volume = _mediaPlaybackService.Volume;
    }

    [RelayCommand]
    private void SelectEventsPanelTab(string tabKey)
    {
        SelectedEventsPanelTab = string.IsNullOrWhiteSpace(tabKey) ? "EventTypes" : tabKey;
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
        TagStartFrame = CurrentFrame;
        TagEndFrame = CurrentFrame;
        TagPlayer = string.Empty;
        TagPeriod = string.Empty;
        TagNotes = string.Empty;
        if (TagTeamSide == TeamSide.Unknown)
        {
            TagTeamSide = TeamSide.Neutral;
        }

        IsTagEventEditorOpen = true;
        StatusMessage = $"Event '{preset.Name}' started.";
    }

    [RelayCommand]
    private void OpenNewPresetEditor()
    {
        IsEditingPreset = false;
        SelectedPreset = null;
        EventTypeName = string.Empty;
        EventTypeHotkey = string.Empty;
        EventTypeColor = "#FFB300";
        EventTypeCategory = "Custom";
        EventTypeIconKey = "event";
        IsPresetEditorOpen = true;
    }

    [RelayCommand]
    private void ClosePresetEditor()
    {
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

        TagStartFrame = CurrentFrame;
        TagEndFrame = CurrentFrame;
        TagTeamSide = TeamSide.Neutral;
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
                ? "Сначала выберите проект."
                : "Проектов пока нет.";
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
        StatusMessage = "Переход к импорту видео пока не реализован.";
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
                    MoveVideoToProjectFolder: true),
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
            string.IsNullOrWhiteSpace(EventTypeIconKey) ? "event" : EventTypeIconKey.Trim());

        await _repository.UpsertTagPresetAsync(preset, CancellationToken.None);
        TagPresets.Add(preset);
        SelectedPreset = preset;
        IsEditingPreset = true;
        IsPresetEditorOpen = false;
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
            IconKey = string.IsNullOrWhiteSpace(EventTypeIconKey) ? "event" : EventTypeIconKey.Trim()
        };

        await _repository.UpsertTagPresetAsync(updatedPreset, CancellationToken.None);

        var selectedIndex = TagPresets.IndexOf(SelectedPreset);
        if (selectedIndex >= 0)
        {
            TagPresets[selectedIndex] = updatedPreset;
        }

        SelectedPreset = updatedPreset;
        IsPresetEditorOpen = false;
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

        if (SelectedPreset.IsSystem)
        {
            StatusMessage = "System event types cannot be deleted.";
            return;
        }

        var presetToDelete = SelectedPreset;
        await _repository.DeleteTagPresetAsync(_projectId, presetToDelete.Id, CancellationToken.None);
        TagPresets.Remove(presetToDelete);
        SelectedPreset = TagPresets.FirstOrDefault();
        IsPresetEditorOpen = false;
        IsEditingPreset = false;
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

        var tagEvent = new TagEvent(
            eventId,
            _projectId,
            SelectedPreset.Id,
            Math.Max(0, TagStartFrame),
            Math.Max(TagStartFrame, TagEndFrame),
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
                TeamSide = tagEvent.TeamSide.ToString(),
                StartFrame = tagEvent.StartFrame,
                EndFrame = tagEvent.EndFrame,
                Player = tagEvent.Player ?? string.Empty,
                Period = tagEvent.Period ?? string.Empty,
                Notes = tagEvent.Notes ?? string.Empty,
                IsSelectedForPlaylist = _selectedPlaylistTagEventIds.Contains(tagEvent.Id)
            });
        }

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
            ? "Подборка очищена."
            : $"Выбрано событий для подборки: {_selectedPlaylistTagEventIds.Count}.";
        OnPropertyChanged(nameof(HasPlaylistSelection));
        OnPropertyChanged(nameof(CanCreatePlaylist));
        OnPropertyChanged(nameof(SelectedPlaylistEventCount));
    }

    [RelayCommand]
    private async Task CreatePlaylistAsync()
    {
        if (_projectId == Guid.Empty)
        {
            StatusMessage = "Сначала откройте проект.";
            return;
        }

        if (_selectedPlaylistTagEventIds.Count == 0)
        {
            StatusMessage = "Сначала выберите события для подборки.";
            return;
        }

        var request = new CreatePlaylistRequestDto(
            _projectId,
            string.IsNullOrWhiteSpace(PlaylistName) ? $"Подборка {DateTime.Now:dd.MM HH:mm}" : PlaylistName.Trim(),
            _selectedPlaylistTagEventIds.ToList(),
            PreRollFrames,
            PostRollFrames,
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
            StatusMessage = $"Не удалось создать плейлист: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenSelectedPlaylistAsync()
    {
        if (SelectedPlaylist is null)
        {
            StatusMessage = "Выберите плейлист.";
            return;
        }

        var playlist = await _playlistService.GetPlaylistAsync(_projectId, SelectedPlaylist.Id, CancellationToken.None);
        if (playlist is null)
        {
            StatusMessage = "Плейлист не найден.";
            await RefreshPlaylistsAsync(CancellationToken.None);
            return;
        }

        ApplyLoadedPlaylist(playlist);
        StatusMessage = $"Плейлист '{playlist.Name}' открыт.";
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
        StatusMessage = $"Переход к клипу '{item.Label}'.";
    }

    [RelayCommand]
    private void PlayActivePlaylist()
    {
        if (_activePlaylistSegments.Count == 0)
        {
            StatusMessage = "Сначала откройте или создайте плейлист.";
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
        StatusMessage = "Воспроизведение плейлиста остановлено.";
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

        StatusMessage = "Настройки облака сохранены.";
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
                UpdatedAtText = $"Обновлен {project.UpdatedAtUtc.ToLocalTime():dd.MM.yyyy}"
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
        ProjectName = "Hockey Analysis";
        TagPresets.Clear();
        TagEvents.Clear();
        Annotations.Clear();
        Playlists.Clear();
        PlaylistItems.Clear();
        _selectedPlaylistTagEventIds.Clear();
        _activePlaylistSegments = [];
        _activePlaylistSegmentIndex = -1;
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
        PlaylistName = "Новая подборка";
        PlaylistDescription = string.Empty;
        ClipSummary = "Segments: 0";
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

        return "Команды еще не указаны";
    }

    private static string FormatProjectSummary(Project project, ProjectVideo? projectVideo)
    {
        if (!string.IsNullOrWhiteSpace(project.Description))
        {
            return project.Description!;
        }

        if (projectVideo is not null)
        {
            return $"Видео: {projectVideo.Title}";
        }

        return "Проект готов к разбору.";
    }

    private async Task LoadProjectAsync(Project project, CancellationToken cancellationToken)
    {
        _selectedPlaylistTagEventIds.Clear();
        _activePlaylistSegments = [];
        _activePlaylistSegmentIndex = -1;
        _lastSegments = [];
        IsPlaylistPlaybackActive = false;
        Playlists.Clear();
        PlaylistItems.Clear();
        SelectedPlaylist = null;
        SelectedPlaylistItem = null;
        ClipSummary = "Segments: 0";
        _projectId = project.Id;
        ProjectName = project.Name;
        PlaylistName = $"{project.Name} playlist";
        PlaylistDescription = string.Empty;
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

        SelectedPreset = TagPresets.FirstOrDefault();
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
                Description = string.IsNullOrWhiteSpace(playlist.Description) ? "Без описания" : playlist.Description,
                ItemCount = playlist.ItemCount,
                UpdatedAtText = $"{playlist.ItemCount} клипов • {playlist.UpdatedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}"
            });
        }

        SelectedPlaylist = Playlists.FirstOrDefault((item) => item.Id == SelectedPlaylist?.Id) ?? Playlists.FirstOrDefault();
    }

    private void ApplyLoadedPlaylist(PlaylistDetailsDto playlist)
    {
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
                Player = string.IsNullOrWhiteSpace(item.Player) ? "Без игрока" : item.Player,
                TeamSide = item.TeamSide.ToString(),
                ClipStartFrame = item.ClipStartFrame,
                ClipEndFrame = item.ClipEndFrame,
                FrameRangeText = $"{item.ClipStartFrame} → {item.ClipEndFrame}"
            });
        }

        SelectedPlaylist = Playlists.FirstOrDefault((candidate) => candidate.Id == playlist.Id) ?? SelectedPlaylist;
        SelectedPlaylistItem = PlaylistItems.FirstOrDefault();
        OnPropertyChanged(nameof(CanPlayActivePlaylist));
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
        StatusMessage = $"Плейлист: клип {index + 1}/{_activePlaylistSegments.Count} '{segment.Label}'.";
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
            StatusMessage = "Плейлист воспроизведен полностью.";
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
        IsPlaying = _mediaPlaybackService.IsPlaying;
        OnPropertyChanged(nameof(CurrentTimeText));
        OnPropertyChanged(nameof(DurationTimeText));
    }

    public void OpenPresetEditor(TagPreset preset)
    {
        SelectedPreset = preset;
        IsEditingPreset = true;
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
            ? parsedTeamSide
            : TeamSide.Neutral;
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
