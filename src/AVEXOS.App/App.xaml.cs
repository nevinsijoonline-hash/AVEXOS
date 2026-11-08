using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using AVEXOS.Core;
using AVEXOS.Security;
using AVEXOS.ThemeEngine;
using AVEXOS.Backup;
using AVEXOS.WindowsIntegration;

namespace AVEXOS.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static readonly string BootLog = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AVEXOS", "logs", "bootstrap.log");

    internal static void Trace(string msg)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BootLog)!);
            File.AppendAllText(BootLog, $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}");
        }
        catch { }
    }

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            Trace($"UNHANDLED: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (s, e) =>
            Trace($"UNOBSERVED TASK: {e.Exception}");
        this.UnhandledException += (s, e) =>
            Trace($"WINUI UNHANDLED: {e.Message} | {e.Exception}");

        InitializeComponent();
        ConfigureServices();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPathPolicy>(sp => new ProtectedPathPolicy());
        services.AddSingleton<IFileSystemGuard, FileSystemGuard>();
        services.AddSingleton<IAuditLog>(sp => new SqliteStore(AvexPaths.DatabasePath, sp.GetRequiredService<IClock>()));
        services.AddSingleton<IOperationStore>(sp => (SqliteStore)sp.GetRequiredService<IAuditLog>());
        services.AddSingleton<ISettingsStore>(sp => (SqliteStore)sp.GetRequiredService<IAuditLog>());
        services.AddSingleton<IBackupStore, JsonBackupStore>();
        services.AddSingleton<IWallpaperService, WallpaperService>();
        services.AddSingleton<IAppDiscoveryService, AppDiscoveryService>();
        services.AddSingleton<IThemeValidator, ThemeValidator>();
        services.AddSingleton<IThemeCatalog, BuiltInThemeCatalog>();
        services.AddSingleton<LocalProposalService>();
        services.AddSingleton<ChangePipeline>();
        services.AddSingleton<ThemeService>();
        Services = services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AvexPaths.EnsureCreated();
        var window = new MainWindow();
        Main = window;
        window.Activate();
    }

    public static MainWindow? Main { get; private set; }
}
