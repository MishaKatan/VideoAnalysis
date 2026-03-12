using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using LibVLCSharp.Avalonia;
using System.ComponentModel;
using System.Reflection;
using VideoAnalysis.App.ViewModels.Shell;

namespace VideoAnalysis.App.Views;

public partial class MainWindow : Window
{
    private static readonly FieldInfo? VideoViewPlatformHandleField =
        typeof(VideoView).GetField("_platformHandle", BindingFlags.Instance | BindingFlags.NonPublic);

    private MainWindowViewModel? _viewModel;
    private bool _isSynchronizingMenus;
    private bool _embeddedHandleBound;
    private bool _isSeekDragging;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        LayoutUpdated += OnLayoutUpdated;
        Opened += OnOpened;
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
            PlayerView.MediaPlayer = _viewModel.MediaPlayer;
            TryBindEmbeddedVideoOutput();
            UpdateSeekBarVisuals();
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            PlayerView.MediaPlayer = _viewModel.MediaPlayer;
            TryBindEmbeddedVideoOutput();
            await _viewModel.InitializeCommand.ExecuteAsync(null);
            TryBindEmbeddedVideoOutput();
            UpdateSeekBarVisuals();
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        TryBindEmbeddedVideoOutput();
    }

    private void TryBindEmbeddedVideoOutput()
    {
        if (_embeddedHandleBound || DataContext is not MainWindowViewModel viewModel || VideoViewPlatformHandleField is null)
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

    private void OnFileMenuActionClick(object? sender, RoutedEventArgs e)
    {
        FileMenuButton.IsChecked = false;
    }

    private void OnViewMenuActionClick(object? sender, RoutedEventArgs e)
    {
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
        }
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
}
