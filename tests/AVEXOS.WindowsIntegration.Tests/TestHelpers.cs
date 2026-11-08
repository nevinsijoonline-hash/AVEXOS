using AVEXOS.Core;

namespace AVEXOS.WindowsIntegration.Tests;

internal class InMemorySettings : ISettingsStore
{
    private readonly Dictionary<string, string> _store = new();

    public string? Get(string key) => _store.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string value) => _store[key] = value;

    public void SetMany(IReadOnlyDictionary<string, string> values)
    {
        foreach (var pair in values)
            _store[pair.Key] = pair.Value;
    }

    public IReadOnlyDictionary<string, string> GetAll() => _store;
}

internal class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
}

internal class FakeWallpaperService : IWallpaperService
{
    private readonly Dictionary<string, string> _wallpapers = new();
    private readonly Dictionary<string, string> _appliedWallpapers = new();
    private readonly List<MonitorInfo> _monitors = new();

    public void AddMonitor(string deviceId, bool isPrimary)
    {
        _monitors.Add(new MonitorInfo(deviceId, deviceId, 1920, 1080, 0, 0, isPrimary));
    }

    public void SetMonitorWallpaper(string deviceId, string path)
    {
        _wallpapers[deviceId] = path;
        _appliedWallpapers[deviceId] = path;
    }

    public string GetAppliedWallpaper(string deviceId)
        => _appliedWallpapers.TryGetValue(deviceId, out var v) ? v : "";

    public AppearanceState Read() => new(_wallpapers.Values.FirstOrDefault() ?? "", 10, false);
    public void Apply(AppearanceState state) { }
    public string ImportWallpaper(string sourcePath) => sourcePath;

    public IReadOnlyList<MonitorInfo> GetMonitors() => _monitors;

    public MonitorWallpaperState? ReadMonitor(string deviceId)
    {
        var mon = _monitors.FirstOrDefault(m => m.DeviceId == deviceId);
        if (mon is null) return null;
        var path = _wallpapers.TryGetValue(deviceId, out var p) ? p : "";
        return new MonitorWallpaperState(mon, path, 10, false);
    }

    public void ApplyToMonitor(string deviceId, AppearanceState state)
    {
        _appliedWallpapers[deviceId] = state.WallpaperPath;
    }

    public void ApplyToAllMonitors(AppearanceState state)
    {
        foreach (var m in _monitors)
            _appliedWallpapers[m.DeviceId] = state.WallpaperPath;
    }
}
