using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Tossakan.Data;
using Tossakan.Services;

namespace Tossakan;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
        UnhandledException += (_, e) =>
        {
            Logger.Log($"UNHANDLED: {e.Exception}");
            e.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Log($"UNOBSERVED: {e.Exception}");
            e.SetObserved();
        };
    }

    private static IServiceProvider ConfigureServices()
    {
        AppPaths.EnsureFolders();
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={AppPaths.DatabasePath}"));
        services.AddSingleton<BoardService>();
        services.AddSingleton<AttachmentService>();
        services.AddSingleton<BackgroundImageService>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<DatabaseChangeWatcherService>();
        return services.BuildServiceProvider();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await DatabaseInitializer.InitializeAsync(
            Services.GetRequiredService<IDbContextFactory<AppDbContext>>());
        Services.GetRequiredService<DatabaseChangeWatcherService>().Start();

        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
