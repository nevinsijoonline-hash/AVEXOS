using System.Reflection;
using System.Runtime.InteropServices;

namespace AVEXOS.Core;

/// <summary>
/// Gathers read-only system information for the dashboard.
/// </summary>
public sealed class SystemInfoService
{
    private readonly ISettingsStore _settings;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public SystemInfoService(ISettingsStore settings)
    {
        _settings = settings;
    }

    public string WindowsVersion => RuntimeInformation.OSDescription;

    public string WindowsBuild => $"Build {Environment.OSVersion.Version.Build}";

    public string DotNetVersion => RuntimeInformation.FrameworkDescription;

    public string AppVersion
    {
        get
        {
            var asm = Assembly.GetEntryAssembly();
            var infoVersion = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (infoVersion is not null) return infoVersion.InformationalVersion;
            var version = asm?.GetName().Version;
            return version?.ToString() ?? "0.0.0";
        }
    }

    public string CurrentThemeId => _settings.Get("theme.id") ?? "avex.dark";

    public string CurrentThemeMode => _settings.Get("theme.mode") ?? "dark";

    public string ActiveWallpaperPath
    {
        get
        {
            var saved = _settings.Get("wallpaper.slideshow.paths");
            if (!string.IsNullOrEmpty(saved))
            {
                var paths = saved.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (paths.Length > 0) return paths[0];
            }
            return "(Default)";
        }
    }

    public TimeSpan SessionDuration => DateTimeOffset.UtcNow - _startedAt;

    public string SessionDurationDisplay
    {
        get
        {
            var d = SessionDuration;
            if (d.TotalHours >= 1)
                return $"{(int)d.TotalHours}h {d.Minutes}m";
            if (d.TotalMinutes >= 1)
                return $"{(int)d.TotalMinutes}m {d.Seconds}s";
            return $"{d.Seconds}s";
        }
    }

    public string MachineName => Environment.MachineName;

    public string UserName => Environment.UserName;

    public int ProcessorCount => Environment.ProcessorCount;

    public long TotalMemoryMb => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
}
