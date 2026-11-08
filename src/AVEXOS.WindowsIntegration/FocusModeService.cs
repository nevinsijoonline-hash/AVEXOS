using System.Text.Json;
using AVEXOS.Core;

namespace AVEXOS.WindowsIntegration;

/// <summary>
/// Implements Focus Mode: dims non-primary monitors to reduce distractions.
/// Stores config in ISettingsStore (keys: focusmode.*) and uses IWallpaperService
/// to apply/restore per-monitor wallpapers.
/// </summary>
public sealed class FocusModeService : IFocusModeService
{
    private const string ConfigKey = "focusmode.config";
    private const string SavedStateKey = "focusmode.saved_state";
    private const string EnabledKey = "focusmode.enabled";

    private readonly ISettingsStore _settings;
    private readonly IWallpaperService _wallpaperService;
    private FocusModeConfig? _cachedConfig;

    public FocusModeService(ISettingsStore settings, IWallpaperService wallpaperService)
    {
        _settings = settings;
        _wallpaperService = wallpaperService;
    }

    public FocusModeConfig GetConfig()
    {
        if (_cachedConfig is not null) return _cachedConfig;

        var json = _settings.Get(ConfigKey);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _cachedConfig = JsonSerializer.Deserialize<FocusModeConfig>(json);
                if (_cachedConfig is not null) return _cachedConfig;
            }
            catch { /* corrupt data – fall through to default */ }
        }

        _cachedConfig = FocusModeConfig.Default;
        return _cachedConfig;
    }

    public void SaveConfig(FocusModeConfig config)
    {
        config.Validate();
        _cachedConfig = config;
        var json = JsonSerializer.Serialize(config);
        _settings.Set(ConfigKey, json);
    }

    public bool IsEnabled
    {
        get
        {
            var val = _settings.Get(EnabledKey);
            return val == bool.TrueString;
        }
    }

    public void Enable()
    {
        if (IsEnabled) return;

        var config = GetConfig();
        config.IsEnabled = true;
        SaveConfig(config);

        // Save original wallpaper state before dimming
        SaveOriginalWallpaperState();

        // Apply dimming to non-primary monitors
        if (config.DimNonPrimary)
        {
            ApplyDimming(config);
        }

        _settings.Set(EnabledKey, bool.TrueString);
    }

    public void Disable()
    {
        if (!IsEnabled) return;

        var config = GetConfig();
        config.IsEnabled = false;
        SaveConfig(config);

        // Restore original wallpapers
        RestoreOriginalWallpaperState();

        _settings.Set(EnabledKey, bool.FalseString);
    }

    private void ApplyDimming(FocusModeConfig config)
    {
        var monitors = _wallpaperService.GetMonitors();
        var primary = MonitorDetection.GetPrimaryMonitor(monitors);
        if (primary is null) return;

        foreach (var monitor in monitors)
        {
            // Skip the primary monitor
            if (monitor.IsPrimary) continue;

            if (config.UseSolidColor)
            {
                // Apply solid color wallpaper
                var solidPath = CreateSolidColorImage(config.SolidColor);
                _wallpaperService.ApplyToMonitor(monitor.DeviceId,
                    new AppearanceState(solidPath, 10, false));
            }
            else
            {
                // Apply dimmed version - use a semi-transparent overlay approach
                // Since we can't easily create dimmed images without GDI+, we use
                // the solid color approach with the dim level as opacity hint
                var dimColor = CalculateDimColor(config.DimLevel);
                var dimPath = CreateSolidColorImage(dimColor);
                _wallpaperService.ApplyToMonitor(monitor.DeviceId,
                    new AppearanceState(dimPath, 10, false));
            }
        }
    }

    private static string CalculateDimColor(int dimLevel)
    {
        // dimLevel 0 = fully black, 100 = original (no dim)
        // We simulate dimming by creating a dark gray color
        var brightness = (int)(255 * (100 - dimLevel) / 100.0);
        brightness = Math.Clamp(brightness, 0, 255);
        var hex = brightness.ToString("X2");
        return $"#{hex}{hex}{hex}";
    }

    private string CreateSolidColorImage(string hexColor)
    {
        // Create a minimal 1x1 BMP file with the specified color
        // This is a simple approach - in production you'd use GDI+ or similar
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "AVEXOS",
            "focusmode",
            $"dim_{hexColor.GetHashCode():X8}.bmp");

        var dir = Path.GetDirectoryName(tempPath);
        if (dir is not null) Directory.CreateDirectory(dir);

        // Write a minimal 1x1 24-bit BMP
        if (!File.Exists(tempPath))
        {
            WriteMinimalBmp(tempPath, hexColor);
        }

        return tempPath;
    }

    private static void WriteMinimalBmp(string path, string hexColor)
    {
        // Parse hex color (#RRGGBB)
        var r = (byte)Convert.ToInt32(hexColor.Substring(1, 2), 16);
        var g = (byte)Convert.ToInt32(hexColor.Substring(3, 2), 16);
        var b = (byte)Convert.ToInt32(hexColor.Substring(5, 2), 16);

        // Minimal 1x1 24-bit BMP file structure
        // BMP header (14 bytes) + DIB header (40 bytes) + pixel data (4 bytes padded)
        var fileSize = 14 + 40 + 4;
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        // BMP file header
        bw.Write((byte)'B');
        bw.Write((byte)'M');
        bw.Write(fileSize);       // File size
        bw.Write((short)0);       // Reserved
        bw.Write((short)0);       // Reserved
        bw.Write(54);             // Pixel data offset

        // DIB header (BITMAPINFOHEADER)
        bw.Write(40);             // Header size
        bw.Write(1);              // Width
        bw.Write(1);              // Height
        bw.Write((short)1);       // Color planes
        bw.Write((short)24);      // Bits per pixel
        bw.Write(0);              // Compression (none)
        bw.Write(4);              // Image size (padded to 4 bytes)
        bw.Write(2835);           // H resolution (72 DPI)
        bw.Write(2835);           // V resolution (72 DPI)
        bw.Write(0);              // Colors in palette
        bw.Write(0);              // Important colors

        // Pixel data (BGR format, row-padded to 4 bytes)
        bw.Write(b);
        bw.Write(g);
        bw.Write(r);
        bw.Write((byte)0);        // Padding
    }

    private void SaveOriginalWallpaperState()
    {
        var monitors = _wallpaperService.GetMonitors();
        var wallpapers = new Dictionary<string, string>();

        foreach (var monitor in monitors)
        {
            var state = _wallpaperService.ReadMonitor(monitor.DeviceId);
            wallpapers[monitor.DeviceId] = state?.WallpaperPath ?? "";
        }

        var savedState = new FocusModeSavedState(wallpapers);
        var json = JsonSerializer.Serialize(savedState);
        _settings.Set(SavedStateKey, json);
    }

    private void RestoreOriginalWallpaperState()
    {
        var json = _settings.Get(SavedStateKey);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var saved = JsonSerializer.Deserialize<FocusModeSavedState>(json);
            if (saved is null) return;

            foreach (var (deviceId, wallpaperPath) in saved.MonitorWallpapers)
            {
                if (!string.IsNullOrEmpty(wallpaperPath))
                {
                    _wallpaperService.ApplyToMonitor(deviceId,
                        new AppearanceState(wallpaperPath, 10, false));
                }
            }

            _settings.Set(SavedStateKey, "");
        }
        catch { /* corrupt saved state – ignore */ }
    }
}
