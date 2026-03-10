using Avalonia.Controls;
using Avalonia.Platform;
using LibVLCSharp.Avalonia;
using System.Reflection;
using VideoAnalysis.App.ViewModels.Shell;

namespace VideoAnalysis.App.Views;

public partial class MainWindow : Window
{
    private static readonly FieldInfo? VideoViewPlatformHandleField =
        typeof(VideoView).GetField("_platformHandle", BindingFlags.Instance | BindingFlags.NonPublic);

    private bool _embeddedHandleBound;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        LayoutUpdated += OnLayoutUpdated;
        Opened += OnOpened;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _embeddedHandleBound = false;
        if (DataContext is MainWindowViewModel viewModel)
        {
            PlayerView.MediaPlayer = viewModel.MediaPlayer;
            TryBindEmbeddedVideoOutput();
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            PlayerView.MediaPlayer = viewModel.MediaPlayer;
            TryBindEmbeddedVideoOutput();
            await viewModel.InitializeCommand.ExecuteAsync(null);
            TryBindEmbeddedVideoOutput();
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
}
