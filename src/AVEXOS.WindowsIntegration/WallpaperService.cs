using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using AVEXOS.Core;
using AVEXOS.Security;

namespace AVEXOS.WindowsIntegration;

public sealed class WallpaperService : IWallpaperService
{
    private readonly IFileSystemGuard _guard;
    private readonly ISettingsStore _settings;
    private NativeMethods.IDesktopWallpaper? _desktopWallpaper;

    public WallpaperService(IFileSystemGuard guard, ISettingsStore settings)
    {
        _guard = guard;
        _settings = settings;
    }

    /// <summary>
    /// Attempts to get the IDesktopWallpaper COM interface for per-monitor support.
    /// Returns null if the interface is not available (e.g., older Windows).
    /// </summary>
    private NativeMethods.IDesktopWallpaper? GetDesktopWallpaper()
    {
        if (_desktopWallpaper is not null) return _desktopWallpaper;

        try
        {
            var instance = Activator.CreateInstance(Type.GetTypeFromCLSID(NativeMethods.CLSID_DesktopWallpaper)!);
            _desktopWallpaper = (NativeMethods.IDesktopWallpaper)instance!;
            return _desktopWallpaper;
        }
        catch
        {
            // IDesktopWallpaper not available; fall back to SystemParametersInfo
            return null;
        }
    }

    public AppearanceState Read()
    {
        var sb = new StringBuilder(32_768);
        NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETDESKWALLPAPER, (uint)sb.Capacity, sb, 0);
        var wallpaper = sb.ToString();

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        var style = int.TryParse(key?.GetValue("WallpaperStyle") as string, out var s) ? s : 10;
        var tile = (key?.GetValue("TileWallpaper") as string) == "1";

        return new AppearanceState(wallpaper, style, tile);
    }

    public void Apply(AppearanceState state)
    {
        if (!RegistryPolicy.IsWritable(@"HKEY_CURRENT_USER\Control Panel\Desktop", out var reason))
        {
            throw new UnauthorizedAccessException($"Registry blocked: {reason}");
        }

        using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop", writable: true);
        key.SetValue("WallpaperStyle", state.WallpaperStyle.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
        key.SetValue("TileWallpaper", state.TileWallpaper ? "1" : "0", RegistryValueKind.String);

        var target = string.IsNullOrWhiteSpace(state.WallpaperPath) ? "" : state.WallpaperPath;
        if (!NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETDESKWALLPAPER, 0, target, NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public string ImportWallpaper(string sourcePath)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Source wallpaper not found.", sourcePath);
        var ext = Path.GetExtension(sourcePath);
        var dest = Path.Combine(AvexPaths.WallpaperDir, $"avex-logo-wallpaper{ext}");
        var guard = _guard.EnsureWritable(dest);
        if (!guard.Allowed) throw new UnauthorizedAccessException($"Blocked by path guard: {guard.Reason}");
        Directory.CreateDirectory(AvexPaths.WallpaperDir);
        File.Copy(sourcePath, guard.CanonicalPath, overwrite: true);
        return guard.CanonicalPath;
    }

    // ── Multi-monitor support ──────────────────────────────────────────

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        return MonitorDetection.GetMonitors();
    }

    public MonitorWallpaperState? ReadMonitor(string deviceId)
    {
        var monitors = GetMonitors();
        var monitor = monitors.FirstOrDefault(m =>
            string.Equals(m.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

        if (monitor is null) return null;

        // Check settings first for per-monitor wallpaper
        var settingsKey = $"wallpaper.monitor.{deviceId}";
        var storedPath = _settings.Get(settingsKey);

        string wallpaperPath;
        if (!string.IsNullOrEmpty(storedPath) && File.Exists(storedPath))
        {
            wallpaperPath = storedPath;
        }
        else
        {
            // Try IDesktopWallpaper API
            var dw = GetDesktopWallpaper();
            if (dw is not null)
            {
                try
                {
                    wallpaperPath = dw.GetWallpaper(deviceId);
                }
                catch
                {
                    wallpaperPath = string.Empty;
                }
            }
            else
            {
                // Fallback: read global wallpaper
                wallpaperPath = Read().WallpaperPath;
            }
        }

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        var style = int.TryParse(key?.GetValue("WallpaperStyle") as string, out var s) ? s : 10;
        var tile = (key?.GetValue("TileWallpaper") as string) == "1";

        return new MonitorWallpaperState(monitor, wallpaperPath, style, tile);
    }

    public void ApplyToMonitor(string deviceId, AppearanceState state)
    {
        var monitors = GetMonitors();
        var monitor = monitors.FirstOrDefault(m =>
            string.Equals(m.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

        if (monitor is null)
            throw new ArgumentException($"Monitor '{deviceId}' not found.", nameof(deviceId));

        // Store per-monitor wallpaper path in settings
        var settingsKey = $"wallpaper.monitor.{deviceId}";
        _settings.Set(settingsKey, state.WallpaperPath);

        // Try IDesktopWallpaper API for per-monitor setting
        var dw = GetDesktopWallpaper();
        if (dw is not null)
        {
            try
            {
                dw.SetWallpaper(deviceId, state.WallpaperPath);
                return;
            }
            catch
            {
                // Fall through to global apply
            }
        }

        // Fallback: apply globally (all monitors get same wallpaper)
        Apply(state);
    }

    public void ApplyToAllMonitors(AppearanceState state)
    {
        var monitors = GetMonitors();

        // Apply via IDesktopWallpaper per-monitor if available
        var dw = GetDesktopWallpaper();
        if (dw is not null)
        {
            var appliedAny = false;
            foreach (var monitor in monitors)
            {
                try
                {
                    dw.SetWallpaper(monitor.DeviceId, state.WallpaperPath);
                    _settings.Set($"wallpaper.monitor.{monitor.DeviceId}", state.WallpaperPath);
                    appliedAny = true;
                }
                catch
                {
                    // Skip monitors that fail
                }
            }

            if (appliedAny)
            {
                // Also update registry for style/tile settings
                ApplyRegistrySettings(state);
                return;
            }
        }

        // Fallback: apply globally
        Apply(state);

        // Store for each monitor in settings
        foreach (var monitor in monitors)
        {
            _settings.Set($"wallpaper.monitor.{monitor.DeviceId}", state.WallpaperPath);
        }
    }

    private static void ApplyRegistrySettings(AppearanceState state)
    {
        if (!RegistryPolicy.IsWritable(@"HKEY_CURRENT_USER\Control Panel\Desktop", out _))
            return;

        using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop", writable: true);
        key.SetValue("WallpaperStyle", state.WallpaperStyle.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
        key.SetValue("TileWallpaper", state.TileWallpaper ? "1" : "0", RegistryValueKind.String);
    }
}
