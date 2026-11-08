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
        services.AddSingleton<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
        services.AddSingleton<IWorkspaceProfileService, WorkspaceProfileService>();
        services.AddSingleton<ThemeSchedulerService>();
        services.AddSingleton<KeyboardShortcutService>();
        services.AddSingleton<SystemInfoService>();
        services.AddSingleton<IPresentationModeService, PresentationModeService>();
        services.AddSingleton<IFocusModeService, FocusModeService>();
        services.AddSingleton<IAutoBrightnessService>(sp =>
            new AutoBrightnessService(
                sp.GetRequiredService<ISettingsStore>(),
                sp.GetRequiredService<IClock>()));
        Services = services.BuildServiceProvider();

        // Start the theme scheduler
        Services.GetRequiredService<ThemeSchedulerService>().Start();

        // Start auto-brightness if enabled
        var autoBrightness = Services.GetRequiredService<IAutoBrightnessService>();
        if (autoBrightness.IsEnabled)
            autoBrightness.Start();
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

        // Apply default wallpaper if none is set
        InitialiseWallpaper();
    }

    private static void InitialiseWallpaper()
    {
        try
        {
            var wallpaper = Services.GetRequiredService<IWallpaperService>();
            var current = wallpaper.Read();
            
            // If no wallpaper is set or it's a blank/empty path, apply the default
            if (string.IsNullOrWhiteSpace(current.WallpaperPath) || !File.Exists(current.WallpaperPath))
            {
                var defaultWallpaper = Path.Combine(AvexPaths.WallpaperDir, "avex-logo-wallpaper.png");
                if (File.Exists(defaultWallpaper))
                {
                    Trace($"Applying default wallpaper: {defaultWallpaper}");
                    var newState = new AppearanceState(defaultWallpaper, 10, false); // 10 = Fit
                    wallpaper.ApplyToAllMonitors(newState);
                }
            }
        }
        catch (Exception ex)
        {
            Trace($"Wallpaper initialization failed: {ex.Message}");
        }
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
            getCurrentThemeMode: () => themeService.CurrentMode,
            onTogglePresentationMode: () =>
            {
                try
                {
                    var presService = Services.GetRequiredService<IPresentationModeService>();
                    if (presService.IsEnabled)
                    {
                        presService.Disable();
                        _trayIcon?.UpdatePresentationModeState(false);
                        Trace("Presentation mode disabled via tray menu");
                    }
                    else
                    {
                        presService.Enable();
                        _trayIcon?.UpdatePresentationModeState(true);
                        Trace("Presentation mode enabled via tray menu");
                    }
                }
                catch (Exception ex)
                {
                    Trace($"Toggle presentation mode from tray failed: {ex.Message}");
                }
            }
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

        // Toggle presentation mode
        shortcuts.TogglePresentationModeAction = () =>
        {
            try
            {
                var presService = Services.GetRequiredService<IPresentationModeService>();
                if (presService.IsEnabled)
                {
                    presService.Disable();
                    _trayIcon?.UpdatePresentationModeState(false);
                    Trace("Presentation mode disabled via shortcut");
                }
                else
                {
                    presService.Enable();
                    _trayIcon?.UpdatePresentationModeState(true);
                    Trace("Presentation mode enabled via shortcut");
                }
            }
            catch (Exception ex)
            {
                Trace($"Toggle presentation mode failed: {ex.Message}");
            }
        };

        // Toggle focus mode
        shortcuts.ToggleFocusModeAction = () =>
        {
            try
            {
                var focusService = Services.GetRequiredService<IFocusModeService>();
                if (focusService.IsEnabled)
                {
                    focusService.Disable();
                    Trace("Focus mode disabled via shortcut");
                }
                else
                {
                    focusService.Enable();
                    Trace("Focus mode enabled via shortcut");
                }
            }
            catch (Exception ex)
            {
                Trace($"Toggle focus mode failed: {ex.Message}");
            }
        };

        shortcuts.Start();
    }

    public static MainWindow? Main { get; private set; }
}
