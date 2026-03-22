using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using VideoAnalysis.App.Configuration;
using VideoAnalysis.App.ViewModels.Shell;
using VideoAnalysis.App.Views;
using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Services;
using VideoAnalysis.Infrastructure.Media;
using VideoAnalysis.Infrastructure.Persistence;
using VideoAnalysis.Infrastructure.Services;

namespace VideoAnalysis.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serviceProvider = ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>(),
                WindowState = WindowState.Maximized
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        var documentsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Video Analytics");
        var projectsRootPath = Path.Combine(documentsRoot, "Projects");
        var legacyAppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoAnalysis");
        var legacyDatabasePath = Path.Combine(legacyAppDataDir, "video-analysis.db");

        Directory.CreateDirectory(documentsRoot);
        Directory.CreateDirectory(projectsRootPath);

        var settingsStore = new AppSettingsStore(Path.Combine(documentsRoot, "settings.json"));
        var settings = settingsStore.Load();

        var services = new ServiceCollection();
        services.AddSingleton(settingsStore);
        services.AddSingleton(settings);
        services.AddSingleton<IProjectRepository>(_ => new SqliteProjectRepository(projectsRootPath, legacyDatabasePath));
        services.AddSingleton<IProjectSetupService>((provider) =>
            new ProjectSetupService(
                provider.GetRequiredService<IProjectRepository>(),
                projectsRootPath));
        services.AddSingleton<ITagService, TagService>();
        services.AddSingleton<IEventCaptureService, EventCaptureService>();
        services.AddSingleton<IPlaylistService, PlaylistService>();
        services.AddSingleton<IMediaPlaybackService, LibVlcMediaPlaybackService>();
        services.AddSingleton<IClipComposerService>(_ => new FfmpegClipComposerService(settings.FfmpegPath));
        services.AddSingleton<IAnnotationRenderService, AnnotationRenderService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
