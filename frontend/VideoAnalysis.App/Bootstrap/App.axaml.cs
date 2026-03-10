using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
                DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoAnalysis");
        Directory.CreateDirectory(appDataDir);
        var settingsStore = new AppSettingsStore(Path.Combine(appDataDir, "settings.json"));
        var settings = settingsStore.Load();
        var databasePath = Path.Combine(appDataDir, "video-analysis.db");
        var projectsRootPath = Path.Combine(appDataDir, "projects");

        var services = new ServiceCollection();
        services.AddSingleton(settingsStore);
        services.AddSingleton(settings);
        services.AddSingleton<IProjectRepository>(_ => new SqliteProjectRepository(databasePath));
        services.AddSingleton<IProjectSetupService>((provider) =>
            new ProjectSetupService(
                provider.GetRequiredService<IProjectRepository>(),
                projectsRootPath));
        services.AddSingleton<ITagService, TagService>();
        services.AddSingleton<IMediaPlaybackService, LibVlcMediaPlaybackService>();
        services.AddSingleton<IClipComposerService>(_ => new FfmpegClipComposerService(settings.FfmpegPath));
        services.AddSingleton<IAnnotationRenderService, AnnotationRenderService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
