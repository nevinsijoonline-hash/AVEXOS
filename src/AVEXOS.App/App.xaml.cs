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
    private static TrayIconService? _trayIcon;

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
        services.AddSingleton<SettingsBackupService>();
        services.AddSingleton<IWallpaperService, WallpaperService>();
        services.AddSingleton<IMonitorSlideshowService, MonitorSlideshowService>();
        services.AddSingleton<IAppDiscoveryService, AppDiscoveryService>();
        services.AddSingleton<IThemeValidator, ThemeValidator>();
        services.AddSingleton<IThemeCatalog, BuiltInThemeCatalog>();
        services.AddSingleton<UserThemeService>();
        services.AddSingleton<LocalProposalService>();
        services.AddSingleton<ChangePipeline>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<ThemeSchedulerService>();
        services.AddSingleton<KeyboardShortcutService>();
        services.AddSingleton<SystemInfoService>();
        Services = services.BuildServiceProvider();

        // Start the theme scheduler
        Services.GetRequiredService<ThemeSchedulerService>().Start();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AvexPaths.EnsureCreated();
        var window = new MainWindow();
        Main = window;
        window.Activate();

        // Wire up and start the global keyboard shortcut service
        InitialiseShortcutService(window);

        // Initialize the system tray icon
        InitialiseTrayIcon(window);
    }

    private static void InitialiseTrayIcon(MainWindow window)
    {
        var themeService = Services.GetRequiredService<ThemeService>();

        _trayIcon = new TrayIconService(
            dispatcher: window.DispatcherQueue,
            onOpen: () => window.ShowAndActivate(),
            onSwitchTheme: mode => themeService.Apply(themeService.CurrentThemeId, mode),
            onNextWallpaper: () =>
            {
                try
                {
                    var slideshow = Services.GetService<IMonitorSlideshowService>();
                    var wallpaper = Services.GetService<IWallpaperService>();
                    var monitors = wallpaper?.GetMonitors();
                    var primary = monitors is not null ? MonitorDetection.GetPrimaryMonitor(monitors) : null;
                    if (slideshow is not null && primary is not null)
                        slideshow.Advance(primary.DeviceId);
                }
                catch { /* non-critical */ }
            },
            onSettings: () => window.NavigateToSettings(),
            onExit: () =>
            {
                _trayIcon?.Dispose();
                window.Close();
                Environment.Exit(0);
            },
            getCurrentThemeMode: () => themeService.CurrentMode
        );

        _trayIcon.Initialize();
        Trace("Tray icon initialized");
    }

    private static void InitialiseShortcutService(MainWindow window)
    {
        var shortcuts = Services.GetRequiredService<KeyboardShortcutService>();
        var themeService = Services.GetRequiredService<ThemeService>();

        // Toggle dark/light mode, keeping the current theme id
        shortcuts.ToggleThemeAction = () =>
        {
            var newMode = themeService.CurrentMode == "dark" ? "light" : "dark";
            themeService.Apply(themeService.CurrentThemeId, newMode);
        };

        // Cycle to the next wallpaper (advances the primary monitor slideshow)
        shortcuts.NextWallpaperAction = () =>
        {
            try
            {
                var slideshow = Services.GetService<IMonitorSlideshowService>();
                var wallpaper = Services.GetService<IWallpaperService>();
                var monitors = wallpaper?.GetMonitors();
                var primary = monitors is not null ? MonitorDetection.GetPrimaryMonitor(monitors) : null;
                if (slideshow is not null && primary is not null)
                    slideshow.Advance(primary.DeviceId);
            }
            catch { /* non-critical */ }
        };

        // Open AVEX control panel (window is already activated by the service)
        shortcuts.NavigateToSettings = () => window.NavigateToSettings();

        shortcuts.Start();
    }

    public static MainWindow? Main { get; private set; }
}
