using System.Text.Json;
using AVEXOS.Core;

namespace AVEXOS.WindowsIntegration;

/// <summary>
/// Manages Presentation Mode: blanks secondary (or all) monitors with a solid color
/// while keeping the primary monitor active with its wallpaper.
/// </summary>
public sealed class PresentationModeService : IPresentationModeService
{
    private const string ConfigEnabledKey = "presentation.enabled";
    private const string ConfigColorKey = "presentation.blankColor";
    private const string ConfigPrimaryTooKey = "presentation.blankPrimaryToo";
    private const string SavedStateKey = "presentation.savedState";

    private readonly ISettingsStore _settings;
    private readonly IWallpaperService _wallpaper;

    public PresentationModeService(ISettingsStore settings, IWallpaperService wallpaper)
    {
        _settings = settings;
        _wallpaper = wallpaper;
    }

    public PresentationModeConfig GetConfig()
    {
        var enabled = _settings.Get(ConfigEnabledKey) == "true";
        var color = _settings.Get(ConfigColorKey) ?? "#000000";
        var primaryToo = _settings.Get(ConfigPrimaryTooKey) == "true";
        return new PresentationModeConfig(enabled, color, primaryToo);
    }

    public void SaveConfig(PresentationModeConfig config)
    {
        _settings.Set(ConfigEnabledKey, config.IsEnabled.ToString().ToLowerInvariant());
        _settings.Set(ConfigColorKey, config.BlankColor);
        _settings.Set(ConfigPrimaryTooKey, config.BlankPrimaryToo.ToString().ToLowerInvariant());
    }

    public bool IsEnabled => _settings.Get(ConfigEnabledKey) == "true";

    public void Enable()
    {
        if (IsEnabled) return;

        // Save current wallpaper state for all monitors
        var monitors = _wallpaper.GetMonitors();
        var savedWallpapers = new Dictionary<string, string>();

        foreach (var monitor in monitors)
        {
            var state = _wallpaper.ReadMonitor(monitor.DeviceId);
            savedWallpapers[monitor.DeviceId] = state?.WallpaperPath ?? string.Empty;
        }

        // Persist saved state
        var savedState = new PresentationModeSavedState(savedWallpapers);
        var json = JsonSerializer.Serialize(savedState);
        _settings.Set(SavedStateKey, json);

        // Mark as enabled in config
        var config = GetConfig();
        _settings.Set(ConfigEnabledKey, "true");

        // Generate solid color image and apply to target monitors
        var blankImagePath = GenerateSolidColorBmp(config.BlankColor);

        foreach (var monitor in monitors)
        {
            // Skip primary if BlankPrimaryToo is false
            if (monitor.IsPrimary && !config.BlankPrimaryToo)
                continue;

            try
            {
                var appearance = new AppearanceState(blankImagePath, 10, false); // 10 = Fit
                _wallpaper.ApplyToMonitor(monitor.DeviceId, appearance);
            }
            catch
            {
                // Skip monitors that fail
            }
        }
    }

    public void Disable()
    {
        if (!IsEnabled) return;

        // Load saved state
        var json = _settings.Get(SavedStateKey);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var savedState = JsonSerializer.Deserialize<PresentationModeSavedState>(json);
                if (savedState is not null)
                {
                    foreach (var (deviceId, wallpaperPath) in savedState.MonitorWallpapers)
                    {
                        if (!string.IsNullOrEmpty(wallpaperPath) && File.Exists(wallpaperPath))
                        {
                            try
                            {
                                var monitorState = _wallpaper.ReadMonitor(deviceId);
                                var style = monitorState?.WallpaperStyle ?? 10;
                                var tile = monitorState?.TileWallpaper ?? false;
                                var appearance = new AppearanceState(wallpaperPath, style, tile);
                                _wallpaper.ApplyToMonitor(deviceId, appearance);
                            }
                            catch
                            {
                                // Skip monitors that fail to restore
                            }
                        }
                    }
                }
            }
            catch
            {
                // Corrupt saved state; ignore
            }
        }

        // Clear saved state and mark as disabled
        _settings.Set(SavedStateKey, "");
        _settings.Set(ConfigEnabledKey, "false");
    }

    /// <summary>
    /// Generates a 1x1 pixel 24-bit BMP image of the specified hex color and saves it to a temp file.
    /// Returns the path to the generated image.
    /// </summary>
    internal static string GenerateSolidColorBmp(string hexColor)
    {
        var (r, g, b) = ParseHexColor(hexColor);
        var tempPath = Path.Combine(Path.GetTempPath(), $"avex-presentation-{Guid.NewGuid():N}.bmp");

        // Write a minimal 1x1 24-bit BMP (58 bytes total)
        // BMP file format: 14-byte file header + 40-byte DIB header + 4-byte pixel data (3 bytes + 1 padding)
        using var fs = File.Create(tempPath);
        using var bw = new BinaryWriter(fs);

        // -- File header (14 bytes) --
        bw.Write((byte)'B');                   // Signature
        bw.Write((byte)'M');
        bw.Write((int)58);                     // File size in bytes
        bw.Write((int)0);                      // Reserved
        bw.Write((int)54);                     // Offset to pixel data

        // -- DIB header (BITMAPINFOHEADER, 40 bytes) --
        bw.Write((int)40);                     // Header size
        bw.Write((int)1);                      // Width
        bw.Write((int)1);                      // Height
        bw.Write((short)1);                    // Color planes
        bw.Write((short)24);                   // Bits per pixel
        bw.Write((int)0);                      // Compression (BI_RGB = 0)
        bw.Write((int)4);                      // Image size (with padding)
        bw.Write((int)2835);                   // X pixels per meter (~72 DPI)
        bw.Write((int)2835);                   // Y pixels per meter
        bw.Write((int)0);                      // Colors in color table
        bw.Write((int)0);                      // Important color count

        // -- Pixel data (BGR order, 1 pixel + 1 byte row padding) --
        bw.Write((byte)b);                     // Blue
        bw.Write((byte)g);                     // Green
        bw.Write((byte)r);                     // Red
        bw.Write((byte)0);                     // Row padding to 4-byte boundary

        return tempPath;
    }

    /// <summary>
    /// Parses a hex color string (e.g., "#FF0000" or "#000000") into (R, G, B) bytes.
    /// Returns (0, 0, 0) for invalid input.
    /// </summary>
    internal static (byte R, byte G, byte B) ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return (0, 0, 0);

        hex = hex.TrimStart('#');

        if (hex.Length == 6)
        {
            try
            {
                var r = Convert.ToByte(hex[0..2], 16);
                var g = Convert.ToByte(hex[2..4], 16);
                var b = Convert.ToByte(hex[4..6], 16);
                return (r, g, b);
            }
            catch
            {
                return (0, 0, 0);
            }
        }

        return (0, 0, 0);
    }
}
