using AVEXOS.Core;

namespace AVEXOS.WindowsIntegration;

/// <summary>
/// Manages per-monitor wallpaper slideshows. Each monitor can have its own
/// independent slideshow with its own image list, interval, and enabled state.
/// </summary>
public sealed class MonitorSlideshowService : IMonitorSlideshowService
{
    private readonly IWallpaperService _wallpaper;
    private readonly ISettingsStore _settings;

    private const string PathsKeyPrefix = "wallpaper.slideshow.monitor.paths.";
    private const string IntervalKeyPrefix = "wallpaper.slideshow.monitor.interval.";
    private const string IndexKeyPrefix = "wallpaper.slideshow.monitor.index.";
    private const string EnabledKeyPrefix = "wallpaper.slideshow.monitor.enabled.";

    public MonitorSlideshowService(IWallpaperService wallpaper, ISettingsStore settings)
    {
        _wallpaper = wallpaper;
        _settings = settings;
    }

    public bool IsEnabled(string monitorDeviceId)
        => _settings.Get(EnabledKeyPrefix + monitorDeviceId) == "true";

    public int GetIntervalMinutes(string monitorDeviceId)
        => int.TryParse(_settings.Get(IntervalKeyPrefix + monitorDeviceId), out var v) ? v : 30;

    public void SetIntervalMinutes(string monitorDeviceId, int minutes)
        => _settings.Set(IntervalKeyPrefix + monitorDeviceId, minutes.ToString());

    public IReadOnlyList<string> GetPaths(string monitorDeviceId)
    {
        var raw = _settings.Get(PathsKeyPrefix + monitorDeviceId);
        if (string.IsNullOrEmpty(raw)) return Array.Empty<string>();
        return raw.Split('|', StringSplitOptions.RemoveEmptyEntries);
    }

    public void SetPaths(string monitorDeviceId, IReadOnlyList<string> paths)
    {
        _settings.Set(PathsKeyPrefix + monitorDeviceId, string.Join('|', paths));
        _settings.Set(IndexKeyPrefix + monitorDeviceId, "0");
    }

    public void Enable(string monitorDeviceId)
    {
        _settings.Set(EnabledKeyPrefix + monitorDeviceId, "true");
        ApplyCurrent(monitorDeviceId);
    }

    public void Disable(string monitorDeviceId)
        => _settings.Set(EnabledKeyPrefix + monitorDeviceId, "false");

    public string? Advance(string monitorDeviceId)
    {
        if (!IsEnabled(monitorDeviceId)) return null;
        var paths = GetPaths(monitorDeviceId);
        if (paths.Count == 0) return null;

        var currentIndex = GetCurrentIndex(monitorDeviceId);
        var nextIndex = (currentIndex + 1) % paths.Count;
        SetCurrentIndex(monitorDeviceId, nextIndex);
        return ApplyCurrent(monitorDeviceId);
    }

    /// <summary>Advances the slideshow on all enabled monitors. Returns the last applied path.</summary>
    public string? Advance()
    {
        // Discover all monitor keys from settings
        string? lastApplied = null;
        foreach (var (key, value) in _settings.GetAll())
        {
            if (key.StartsWith(EnabledKeyPrefix) && value == "true")
            {
                var deviceId = key[EnabledKeyPrefix.Length..];
                var result = Advance(deviceId);
                if (result is not null) lastApplied = result;
            }
        }
        return lastApplied;
    }

    public string? ApplyCurrent(string monitorDeviceId)
    {
        var paths = GetPaths(monitorDeviceId);
        if (paths.Count == 0) return null;

        var idx = GetCurrentIndex(monitorDeviceId) % paths.Count;
        var path = paths[idx];
        if (!File.Exists(path)) return null;

        try
        {
            var imported = _wallpaper.ImportWallpaper(path);
            var monitorState = _wallpaper.ReadMonitor(monitorDeviceId);
            var style = monitorState?.WallpaperStyle ?? 10;
            var tile = monitorState?.TileWallpaper ?? false;
            var newState = new AppearanceState(imported, style, tile);
            _wallpaper.ApplyToMonitor(monitorDeviceId, newState);
            return path;
        }
        catch
        {
            return null;
        }
    }

    public void Reset(string monitorDeviceId)
    {
        Disable(monitorDeviceId);
        _settings.Set(PathsKeyPrefix + monitorDeviceId, "");
        _settings.Set(IndexKeyPrefix + monitorDeviceId, "0");
    }

    private int GetCurrentIndex(string monitorDeviceId)
        => int.TryParse(_settings.Get(IndexKeyPrefix + monitorDeviceId), out var v) ? v : 0;

    private void SetCurrentIndex(string monitorDeviceId, int index)
        => _settings.Set(IndexKeyPrefix + monitorDeviceId, index.ToString());
}
