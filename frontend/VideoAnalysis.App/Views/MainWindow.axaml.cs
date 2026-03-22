using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LibVLCSharp.Avalonia;
using System.ComponentModel;
using System.Reflection;
using VideoAnalysis.App.ViewModels.Items;
using VideoAnalysis.App.ViewModels.Shell;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.App.Views;

public partial class MainWindow : Window
{
    private static readonly FieldInfo? VideoViewPlatformHandleField =
        typeof(VideoView).GetField("_platformHandle", BindingFlags.Instance | BindingFlags.NonPublic);

    private ToggleButton FileMenuButton => this.FindControl<ToggleButton>(nameof(FileMenuButton))
        ?? throw new InvalidOperationException("FileMenuButton was not found.");
    private ToggleButton ViewMenuButton => this.FindControl<ToggleButton>(nameof(ViewMenuButton))
        ?? throw new InvalidOperationException("ViewMenuButton was not found.");
    private ToggleButton HelpMenuButton => this.FindControl<ToggleButton>(nameof(HelpMenuButton))
        ?? throw new InvalidOperationException("HelpMenuButton was not found.");
    private Border PlayerSurfaceHost => this.FindControl<Border>(nameof(PlayerSurfaceHost))
        ?? throw new InvalidOperationException("PlayerSurfaceHost was not found.");
    private Border EventsPanel => this.FindControl<Border>(nameof(EventsPanel))
        ?? throw new InvalidOperationException("EventsPanel was not found.");
    private GridSplitter EventsPanelSplitter => this.FindControl<GridSplitter>(nameof(EventsPanelSplitter))
        ?? throw new InvalidOperationException("EventsPanelSplitter was not found.");
    private Border AnalysisPanel => this.FindControl<Border>(nameof(AnalysisPanel))
        ?? throw new InvalidOperationException("AnalysisPanel was not found.");
    private GridSplitter AnalysisPanelSplitter => this.FindControl<GridSplitter>(nameof(AnalysisPanelSplitter))
        ?? throw new InvalidOperationException("AnalysisPanelSplitter was not found.");
    private Border TimelinePanel => this.FindControl<Border>(nameof(TimelinePanel))
        ?? throw new InvalidOperationException("TimelinePanel was not found.");
    private GridSplitter TimelinePanelSplitter => this.FindControl<GridSplitter>(nameof(TimelinePanelSplitter))
        ?? throw new InvalidOperationException("TimelinePanelSplitter was not found.");
    private Grid MainLayoutGrid => this.FindControl<Grid>(nameof(MainLayoutGrid))
        ?? throw new InvalidOperationException("MainLayoutGrid was not found.");
    private ColumnDefinition LeftPanelColumn => MainLayoutGrid.ColumnDefinitions[0];
    private ColumnDefinition EventsPanelSplitterColumn => MainLayoutGrid.ColumnDefinitions[1];
    private ColumnDefinition EventsPanelColumn => MainLayoutGrid.ColumnDefinitions[2];
    private ColumnDefinition AnalysisPanelSplitterColumn => MainLayoutGrid.ColumnDefinitions[3];
    private ColumnDefinition AnalysisPanelColumn => MainLayoutGrid.ColumnDefinitions[4];
    private RowDefinition TopContentRow => MainLayoutGrid.RowDefinitions[0];
    private RowDefinition TimelineSplitterRow => MainLayoutGrid.RowDefinitions[1];
    private RowDefinition TimelineRow => MainLayoutGrid.RowDefinitions[2];
    private VideoView PlayerView => this.FindControl<VideoView>(nameof(PlayerView))
        ?? throw new InvalidOperationException("PlayerView was not found.");
    private Grid SeekBarRoot => this.FindControl<Grid>(nameof(SeekBarRoot))
        ?? throw new InvalidOperationException("SeekBarRoot was not found.");
    private Border SeekBarProgress => this.FindControl<Border>(nameof(SeekBarProgress))
        ?? throw new InvalidOperationException("SeekBarProgress was not found.");
    private Ellipse SeekBarThumb => this.FindControl<Ellipse>(nameof(SeekBarThumb))
        ?? throw new InvalidOperationException("SeekBarThumb was not found.");
    private Border PresetEditorDialog => this.FindControl<Border>(nameof(PresetEditorDialog))
        ?? throw new InvalidOperationException("PresetEditorDialog was not found.");
    private Button PresetEditorCloseButton => this.FindControl<Button>(nameof(PresetEditorCloseButton))
        ?? throw new InvalidOperationException("PresetEditorCloseButton was not found.");
    private Border TagEventEditorDialog => this.FindControl<Border>(nameof(TagEventEditorDialog))
        ?? throw new InvalidOperationException("TagEventEditorDialog was not found.");
    private Button TagEventEditorCloseButton => this.FindControl<Button>(nameof(TagEventEditorCloseButton))
        ?? throw new InvalidOperationException("TagEventEditorCloseButton was not found.");
    private Button StartupPrimaryButton => this.FindControl<Button>(nameof(StartupPrimaryButton))
        ?? throw new InvalidOperationException("StartupPrimaryButton was not found.");
    private Button NewProjectCloseButton => this.FindControl<Button>(nameof(NewProjectCloseButton))
        ?? throw new InvalidOperationException("NewProjectCloseButton was not found.");

    private MainWindowViewModel? _viewModel;
    private bool _isSynchronizingMenus;
    private bool _embeddedHandleBound;
    private bool _isSeekDragging;
    private bool _isAdjustingEventTypeHotkeyText;
    private double _lastVisibleLeftPanelWidth;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        LayoutUpdated += OnLayoutUpdated;
        Opened += OnOpened;
        KeyDown += OnWindowKeyDown;
        AddHandler(InputElement.PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        _embeddedHandleBound = false;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateVideoSurfaceVisibility();
            UpdatePanelLayout();
            UpdateSeekBarVisuals();
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            UpdateVideoSurfaceVisibility();
            UpdatePanelLayout();
            await _viewModel.InitializeCommand.ExecuteAsync(null);
            UpdateVideoSurfaceVisibility();
            UpdatePanelLayout();
            UpdateSeekBarVisuals();
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        TryBindEmbeddedVideoOutput();
    }

    private void UpdateVideoSurfaceVisibility()
    {
        if (_viewModel is null)
        {
            return;
        }

        var isVisible = _viewModel.IsPlayerSurfaceVisible;
        PlayerSurfaceHost.IsVisible = isVisible;
        PlayerView.IsVisible = isVisible;

        if (!isVisible)
        {
            PlayerView.MediaPlayer = null;
            return;
        }

        PlayerView.MediaPlayer = _viewModel.MediaPlayer;
        _embeddedHandleBound = false;
        TryBindEmbeddedVideoOutput();
    }

    private void TryBindEmbeddedVideoOutput()
    {
        if (_embeddedHandleBound
            || DataContext is not MainWindowViewModel viewModel
            || _viewModel?.IsPlayerSurfaceVisible != true
            || VideoViewPlatformHandleField is null)
        {
            return;
        }

        if (VideoViewPlatformHandleField.GetValue(PlayerView) is not IPlatformHandle platformHandle)
        {
            return;
        }

        if (platformHandle.Handle == IntPtr.Zero)
        {
            return;
        }

        viewModel.ForceAttachVideoHandle(platformHandle.Handle);
        _embeddedHandleBound = true;
    }

    private async void OnFileMenuActionClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is Button { Content: string content })
        {
            switch (content)
            {
                case "����� ������":
                    _viewModel.OpenNewProjectDialogCommand.Execute(null);
                    break;
                case "�������...":
                    await _viewModel.OpenStartupScreenCommand.ExecuteAsync(null);
                    break;
            }
        }

        FileMenuButton.IsChecked = false;
    }

    private void OnViewMenuActionClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is Button button)
        {
            switch (button.Tag as string)
            {
                case "ToggleTimelinePanel":
                    _viewModel.ToggleTimelineVisibilityCommand.Execute(null);
                    break;
                case "ToggleEventsPanel":
                    _viewModel.ToggleEventsPanelVisibilityCommand.Execute(null);
                    break;
                case "ToggleAnalysisPanel":
                    _viewModel.ToggleAnalysisPanelVisibilityCommand.Execute(null);
                    break;
            }
        }

        ViewMenuButton.IsChecked = false;
    }

    private void OnHelpMenuActionClick(object? sender, RoutedEventArgs e)
    {
        HelpMenuButton.IsChecked = false;
    }

    private void OnFileMenuChecked(object? sender, RoutedEventArgs e)
    {
        CloseOtherMenus(FileMenuButton);
    }

    private void OnViewMenuChecked(object? sender, RoutedEventArgs e)
    {
        CloseOtherMenus(ViewMenuButton);
    }

    private void OnHelpMenuChecked(object? sender, RoutedEventArgs e)
    {
        CloseOtherMenus(HelpMenuButton);
    }

    private void OnToggleFullscreenClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.FullScreen
            ? WindowState.Normal
            : WindowState.FullScreen;
    }

    private void OnEventTypeItemDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Control { DataContext: EventTypeItemViewModel eventTypeItem })
        {
            return;
        }

        _viewModel.OpenPresetEditor(eventTypeItem.Preset);
    }

    private void OnTagEventItemDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Control { DataContext: VideoAnalysis.App.ViewModels.Items.TagEventItemViewModel tagEvent })
        {
            return;
        }

        _viewModel.OpenTagEventEditor(tagEvent);
    }

    private void OnTagEventPreviewClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Control { DataContext: VideoAnalysis.App.ViewModels.Items.TagEventItemViewModel tagEvent })
        {
            return;
        }

        _viewModel.SeekToTagEventStart(tagEvent);
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null || ShouldIgnoreHotkeys(e.Source))
        {
            return;
        }

        var hotkey = TryMapHotkey(e);
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return;
        }

        await _viewModel.HandleEventTypeHotkeyAsync(hotkey);
        e.Handled = true;
    }

    private void OnEventTypeHotkeyTextInput(object? sender, TextInputEventArgs e)
    {
        if (_viewModel is null || sender is not TextBox textBox)
        {
            return;
        }

        var replacement = TryExtractSingleEnglishLetter(e.Text);
        e.Handled = true;

        if (replacement is null)
        {
            return;
        }

        _viewModel.EventTypeHotkey = replacement;
        textBox.Text = _viewModel.EventTypeHotkey;
        textBox.CaretIndex = textBox.Text?.Length ?? 0;
    }

    private void OnEventTypeHotkeyEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null || sender is not TextBox textBox)
        {
            return;
        }

        if (e.Key is Key.Back or Key.Delete)
        {
            _viewModel.EventTypeHotkey = string.Empty;
            textBox.Text = string.Empty;
            textBox.CaretIndex = 0;
            e.Handled = true;
        }
    }

    private void OnEventTypeHotkeyTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isAdjustingEventTypeHotkeyText || _viewModel is null || sender is not TextBox textBox)
        {
            return;
        }

        var normalized = TryExtractSingleEnglishLetter(textBox.Text) ?? string.Empty;
        _viewModel.EventTypeHotkey = normalized;
        var finalText = _viewModel.EventTypeHotkey ?? string.Empty;

        if (string.Equals(textBox.Text, finalText, StringComparison.Ordinal))
        {
            return;
        }

        _isAdjustingEventTypeHotkeyText = true;
        textBox.Text = finalText;
        textBox.CaretIndex = finalText.Length;
        _isAdjustingEventTypeHotkeyText = false;
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (!_viewModel.IsPresetEditorOpen && !_viewModel.IsTagEventEditorOpen)
        {
            return;
        }

        if (HasVisualAncestor<TextBox>(e.Source))
        {
            return;
        }

        if (HasVisualAncestor<Button>(e.Source)
            || HasVisualAncestor<ToggleButton>(e.Source)
            || HasVisualAncestor<ComboBox>(e.Source)
            || HasVisualAncestor<ListBoxItem>(e.Source))
        {
            return;
        }

        if (e.Source is Control { Focusable: true })
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel.IsPresetEditorOpen)
            {
                PresetEditorDialog.Focus();
            }
            else if (_viewModel.IsTagEventEditorOpen)
            {
                TagEventEditorDialog.Focus();
            }
        });
    }

    private void OnSeekBarSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateSeekBarVisuals();
    }

    private void OnSeekBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _isSeekDragging = true;
        e.Pointer.Capture((IInputElement?)sender);
        SeekToPointerPosition(e);
    }

    private void OnSeekBarPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isSeekDragging)
        {
            return;
        }

        SeekToPointerPosition(e);
    }

    private void OnSeekBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSeekDragging)
        {
            return;
        }

        SeekToPointerPosition(e);
        e.Pointer.Capture(null);
        _isSeekDragging = false;
    }

    private void SeekToPointerPosition(PointerEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var width = SeekBarRoot.Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        var point = e.GetPosition(SeekBarRoot);
        var ratio = Math.Clamp(point.X / width, 0d, 1d);
        var targetFrame = (long)Math.Round(ratio * Math.Max(1, _viewModel.DurationFrames));
        _viewModel.CurrentFrame = targetFrame;
        UpdateSeekBarVisuals();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.CurrentFrame) or nameof(MainWindowViewModel.DurationFrames))
        {
            UpdateSeekBarVisuals();
            return;
        }

        if (e.PropertyName is nameof(MainWindowViewModel.IsAnalysisPanelVisible) or nameof(MainWindowViewModel.IsEventsPanelVisible) or nameof(MainWindowViewModel.IsTimelineVisible))
        {
            UpdatePanelLayout();
            return;
        }

        if (e.PropertyName is nameof(MainWindowViewModel.IsNewProjectDialogOpen) or nameof(MainWindowViewModel.IsStartupScreenOpen))
        {
            UpdateVideoSurfaceVisibility();
            if (_viewModel?.IsNewProjectDialogOpen == true)
            {
                Dispatcher.UIThread.Post(() => NewProjectCloseButton.Focus());
            }
            else if (_viewModel?.IsStartupScreenVisible == true)
            {
                Dispatcher.UIThread.Post(() => StartupPrimaryButton.Focus());
            }

            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsPresetEditorOpen) && _viewModel?.IsPresetEditorOpen == true)
        {
            Dispatcher.UIThread.Post(() => PresetEditorCloseButton.Focus());
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsTagEventEditorOpen) && _viewModel?.IsTagEventEditorOpen == true)
        {
            Dispatcher.UIThread.Post(() => TagEventEditorCloseButton.Focus());
            return;
        }

    }

    private async void OnBrowseNewProjectVideoClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || StorageProvider is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select video file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Video files")
                {
                    Patterns = ["*.mp4", "*.mov", "*.avi", "*.mkv", "*.m4v"]
                },
                FilePickerFileTypes.All
            ]
        });

        var file = files.FirstOrDefault();
        var localPath = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            _viewModel.NewProjectVideoPath = localPath;
        }
    }

    private void UpdateAnalysisPanelVisibility()
    {
        if (_viewModel is null)
        {
            return;
        }

        var isVisible = _viewModel.IsAnalysisPanelVisible;
        AnalysisPanel.IsVisible = isVisible;
        AnalysisPanelSplitter.IsVisible = isVisible;

        if (isVisible)
        {
            AnalysisPanelColumn.MinWidth = 280;
            LeftPanelColumn.Width = new GridLength(3.2, GridUnitType.Star);
            EventsPanelColumn.Width = new GridLength(1.05, GridUnitType.Star);
            AnalysisPanelSplitterColumn.Width = new GridLength(6);
            AnalysisPanelColumn.Width = new GridLength(1.45, GridUnitType.Star);
            return;
        }

        _lastVisibleLeftPanelWidth = LeftPanelColumn.ActualWidth > 0
            ? LeftPanelColumn.ActualWidth
            : _lastVisibleLeftPanelWidth;

        AnalysisPanelColumn.MinWidth = 0;
        LeftPanelColumn.Width = _lastVisibleLeftPanelWidth > 0
            ? new GridLength(_lastVisibleLeftPanelWidth, GridUnitType.Pixel)
            : new GridLength(3.2, GridUnitType.Star);
        EventsPanelColumn.Width = new GridLength(1, GridUnitType.Star);
        AnalysisPanelSplitterColumn.Width = new GridLength(0);
        AnalysisPanelColumn.Width = new GridLength(0);
    }


    private void UpdatePanelLayout()
    {
        if (_viewModel is null)
        {
            return;
        }

        var isTimelineVisible = _viewModel.IsTimelineVisible;
        var isEventsVisible = _viewModel.IsEventsPanelVisible;
        var isAnalysisVisible = _viewModel.IsAnalysisPanelVisible;

        TimelinePanel.IsVisible = isTimelineVisible;
        TimelinePanelSplitter.IsVisible = isTimelineVisible;
        EventsPanel.IsVisible = isEventsVisible;
        EventsPanelSplitter.IsVisible = isEventsVisible;
        AnalysisPanel.IsVisible = isAnalysisVisible;
        AnalysisPanelSplitter.IsVisible = isAnalysisVisible;

        TopContentRow.MinHeight = 260;
        TopContentRow.Height = new GridLength(1, GridUnitType.Star);
        TimelineSplitterRow.Height = isTimelineVisible ? new GridLength(6) : new GridLength(0);
        TimelineRow.MinHeight = isTimelineVisible ? 170 : 0;
        TimelineRow.Height = isTimelineVisible ? new GridLength(240, GridUnitType.Pixel) : new GridLength(0);

        if (isAnalysisVisible && isEventsVisible)
        {
            AnalysisPanelColumn.MinWidth = 280;
            EventsPanelColumn.MinWidth = 250;
            LeftPanelColumn.Width = new GridLength(3.2, GridUnitType.Star);
            EventsPanelSplitterColumn.Width = new GridLength(6);
            EventsPanelColumn.Width = new GridLength(1.05, GridUnitType.Star);
            AnalysisPanelSplitterColumn.Width = new GridLength(6);
            AnalysisPanelColumn.Width = new GridLength(1.45, GridUnitType.Star);
            return;
        }

        if (isAnalysisVisible && !isEventsVisible)
        {
            AnalysisPanelColumn.MinWidth = 280;
            EventsPanelColumn.MinWidth = 0;
            LeftPanelColumn.Width = new GridLength(3.2, GridUnitType.Star);
            EventsPanelSplitterColumn.Width = new GridLength(0);
            EventsPanelColumn.Width = new GridLength(0);
            AnalysisPanelSplitterColumn.Width = new GridLength(6);
            AnalysisPanelColumn.Width = new GridLength(1.45, GridUnitType.Star);
            return;
        }

        if (!isAnalysisVisible && isEventsVisible)
        {
            _lastVisibleLeftPanelWidth = LeftPanelColumn.ActualWidth > 0
                ? LeftPanelColumn.ActualWidth
                : _lastVisibleLeftPanelWidth;

            AnalysisPanelColumn.MinWidth = 0;
            EventsPanelColumn.MinWidth = 250;

            var totalWidth = MainLayoutGrid.Bounds.Width;
            var splitterWidth = 6d;
            var minimumEventsWidth = 250d;
            var desiredLeftWidth = _lastVisibleLeftPanelWidth > 0
                ? _lastVisibleLeftPanelWidth
                : LeftPanelColumn.ActualWidth;

            if (totalWidth > minimumEventsWidth + splitterWidth)
            {
                desiredLeftWidth = Math.Min(desiredLeftWidth, totalWidth - minimumEventsWidth - splitterWidth);
            }

            if (desiredLeftWidth > 0)
            {
                LeftPanelColumn.Width = new GridLength(desiredLeftWidth, GridUnitType.Pixel);
            }
            else
            {
                LeftPanelColumn.Width = new GridLength(3.2, GridUnitType.Star);
            }

            EventsPanelSplitterColumn.Width = new GridLength(splitterWidth);
            EventsPanelColumn.Width = new GridLength(1, GridUnitType.Star);
            AnalysisPanelSplitterColumn.Width = new GridLength(0);
            AnalysisPanelColumn.Width = new GridLength(0);
            return;
        }

        AnalysisPanelColumn.MinWidth = 0;
        EventsPanelColumn.MinWidth = 0;
        LeftPanelColumn.Width = new GridLength(1, GridUnitType.Star);
        EventsPanelSplitterColumn.Width = new GridLength(0);
        EventsPanelColumn.Width = new GridLength(0);
        AnalysisPanelSplitterColumn.Width = new GridLength(0);
        AnalysisPanelColumn.Width = new GridLength(0);
    }
    private void UpdateSeekBarVisuals()
    {
        if (_viewModel is null)
        {
            return;
        }

        var width = SeekBarRoot.Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        var duration = Math.Max(1, _viewModel.DurationFrames);
        var ratio = Math.Clamp(_viewModel.CurrentFrame / (double)duration, 0d, 1d);
        var progressWidth = width * ratio;
        SeekBarProgress.Width = progressWidth;

        var thumbWidth = SeekBarThumb.Bounds.Width > 0 ? SeekBarThumb.Bounds.Width : SeekBarThumb.Width;
        var thumbX = Math.Clamp(progressWidth - (thumbWidth / 2d), 0d, Math.Max(0d, width - thumbWidth));
        SeekBarThumb.RenderTransform = new TranslateTransform(thumbX, 0);
    }

    private void CloseOtherMenus(ToggleButton activeButton)
    {
        if (_isSynchronizingMenus)
        {
            return;
        }

        _isSynchronizingMenus = true;
        try
        {
            if (!ReferenceEquals(activeButton, FileMenuButton))
            {
                FileMenuButton.IsChecked = false;
            }

            if (!ReferenceEquals(activeButton, ViewMenuButton))
            {
                ViewMenuButton.IsChecked = false;
            }

            if (!ReferenceEquals(activeButton, HelpMenuButton))
            {
                HelpMenuButton.IsChecked = false;
            }
        }
        finally
        {
            _isSynchronizingMenus = false;
        }
    }

    private static bool ShouldIgnoreHotkeys(object? source)
    {
        return source is TextBox;
    }

    private static string? TryMapHotkey(KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return null;
        }

        var keyText = e.Key.ToString();
        if (string.IsNullOrWhiteSpace(keyText))
        {
            return null;
        }

        if (keyText.Length == 1 && char.IsLetterOrDigit(keyText[0]))
        {
            return keyText.ToUpperInvariant();
        }

        if (keyText.Length == 2 && keyText[0] == 'D' && char.IsDigit(keyText[1]))
        {
            return keyText[1].ToString();
        }

        if (keyText.StartsWith("NumPad", StringComparison.Ordinal) && keyText.Length == "NumPad0".Length)
        {
            var lastChar = keyText[^1];
            if (char.IsDigit(lastChar))
            {
                return lastChar.ToString();
            }
        }

        return null;
    }

    private static string? TryExtractSingleEnglishLetter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        for (var index = text.Length - 1; index >= 0; index--)
        {
            var character = text[index];
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

    private static bool HasVisualAncestor<T>(object? source) where T : class
    {
        if (source is not Visual visual)
        {
            return false;
        }

        return visual.GetSelfAndVisualAncestors().OfType<T>().Any();
    }
}








