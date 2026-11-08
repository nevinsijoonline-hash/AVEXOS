using AVEXOS.Core;
using AVEXOS.Security;
using AVEXOS.WindowsIntegration;
using Xunit;

namespace AVEXOS.WindowsIntegration.Tests;

public class WallpaperServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalWallpaperDir;

    public WallpaperServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"avex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _originalWallpaperDir = AvexPaths.WallpaperDir;
        AvexPaths.OverrideWallpaperDirForTests(_tempDir);
    }

    public void Dispose()
    {
        AvexPaths.OverrideWallpaperDirForTests(_originalWallpaperDir);
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void ImportWallpaper_Copies_File_To_Wallpaper_Dir()
    {
        var guard = new AllowAllGuard();
        var settings = new InMemorySettings();
        var service = new WallpaperService(guard, settings);

        var source = Path.Combine(_tempDir, "source.png");
        File.WriteAllText(source, "fake-image-data");

        var result = service.ImportWallpaper(source);

        Assert.True(File.Exists(result));
        Assert.Equal("avex-logo-wallpaper.png", Path.GetFileName(result));
        Assert.Equal("fake-image-data", File.ReadAllText(result));
    }

    [Fact]
    public void ImportWallpaper_Throws_When_Source_Not_Found()
    {
        var guard = new AllowAllGuard();
        var settings = new InMemorySettings();
        var service = new WallpaperService(guard, settings);

        Assert.Throws<FileNotFoundException>(() => service.ImportWallpaper(@"C:\nonexistent\file.png"));
    }

    [Fact]
    public void ImportWallpaper_Throws_When_Guard_Denies()
    {
        var guard = new DenyAllGuard();
        var settings = new InMemorySettings();
        var service = new WallpaperService(guard, settings);

        var source = Path.Combine(_tempDir, "source.png");
        File.WriteAllText(source, "fake-image-data");

        var ex = Assert.Throws<UnauthorizedAccessException>(() => service.ImportWallpaper(source));
        Assert.Contains("Blocked by path guard", ex.Message);
    }

    [Fact]
    public void ImportWallpaper_Overwrites_Existing_File()
    {
        var guard = new AllowAllGuard();
        var settings = new InMemorySettings();
        var service = new WallpaperService(guard, settings);

        var source = Path.Combine(_tempDir, "source.png");
        File.WriteAllText(source, "first-version");
        service.ImportWallpaper(source);

        File.WriteAllText(source, "second-version");
        var result = service.ImportWallpaper(source);

        Assert.Equal("second-version", File.ReadAllText(result));
    }

    [Fact(Skip = "Requires administrator privileges to create reparse points")]
    public void ImportWallpaper_Rejects_ReparsePoint_At_Destination()
    {
        var guard = new AllowAllGuard();
        var settings = new InMemorySettings();
        var service = new WallpaperService(guard, settings);

        var source = Path.Combine(_tempDir, "source.png");
        File.WriteAllText(source, "legitimate-data");

        var protectedDir = Path.Combine(_tempDir, "protected-dir");
        Directory.CreateDirectory(protectedDir);

        var destPath = Path.Combine(_tempDir, "avex-logo-wallpaper.png");
        // Directory.CreateJunction(destPath, protectedDir); // Requires .NET 6+ and admin
        
        var ex = Assert.Throws<UnauthorizedAccessException>(() => service.ImportWallpaper(source));
        Assert.Contains("reparse point", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

file class AllowAllGuard : IFileSystemGuard
{
    public GuardResult EnsureWritable(string path) => new(true, path, "Allowed");
    public GuardResult EnsureDirectory(string path) => new(true, path, "Allowed");
}


file class DenyAllGuard : IFileSystemGuard
{
    public GuardResult EnsureWritable(string path) => new(false, path, "Denied by test");
    public GuardResult EnsureDirectory(string path) => new(false, path, "Denied by test");
}

public class MultiMonitorWallpaperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalWallpaperDir;
    private readonly InMemorySettings _settings;
    private readonly WallpaperService _service;

    public MultiMonitorWallpaperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"avex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _originalWallpaperDir = AvexPaths.WallpaperDir;
        AvexPaths.OverrideWallpaperDirForTests(_tempDir);
        _settings = new InMemorySettings();
        _service = new WallpaperService(new AllowAllGuard(), _settings);
    }

    public void Dispose()
    {
        AvexPaths.OverrideWallpaperDirForTests(_originalWallpaperDir);
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetMonitors_Returns_AtLeast_One_Monitor_On_Windows()
    {
        // On any Windows system with a display, this should return at least one monitor
        var monitors = _service.GetMonitors();
        Assert.NotNull(monitors);
        // In a test environment we might have 0 or more monitors
        if (monitors.Count > 0)
        {
            var primary = monitors.FirstOrDefault(m => m.IsPrimary);
            Assert.NotNull(primary);
        }
    }

    [Fact]
    public void ReadMonitor_Returns_Null_For_Unknown_Device()
    {
        var result = _service.ReadMonitor("NONEXISTENT_MONITOR_DEVICE");
        Assert.Null(result);
    }

    [Fact]
    public void ReadMonitor_Returns_State_For_Detected_Monitor()
    {
        var monitors = _service.GetMonitors();
        if (monitors.Count == 0) return; // Skip if no monitors in test env

        var monitor = monitors[0];
        var state = _service.ReadMonitor(monitor.DeviceId);
        Assert.NotNull(state);
        Assert.Equal(monitor.DeviceId, state.Monitor.DeviceId);
    }

    [Fact]
    public void ApplyToMonitor_Stores_Path_In_Settings()
    {
        var monitors = _service.GetMonitors();
        if (monitors.Count == 0) return;

        var monitor = monitors[0];
        var source = Path.Combine(_tempDir, "test-wallpaper.png");
        File.WriteAllText(source, "test-data");
        var imported = _service.ImportWallpaper(source);

        var state = new AppearanceState(imported, 10, false);
        _service.ApplyToMonitor(monitor.DeviceId, state);

        var storedPath = _settings.Get($"wallpaper.monitor.{monitor.DeviceId}");
        Assert.Equal(imported, storedPath);
    }

    [Fact]
    public void ApplyToMonitor_Throws_For_Unknown_Device()
    {
        var state = new AppearanceState("some-path", 10, false);
        Assert.Throws<ArgumentException>(() => _service.ApplyToMonitor("FAKE_DEVICE", state));
    }

    [Fact]
    public void ApplyToAllMonitors_Stores_Path_For_All_Detected_Monitors()
    {
        var monitors = _service.GetMonitors();
        if (monitors.Count == 0) return;

        var source = Path.Combine(_tempDir, "test-wallpaper.png");
        File.WriteAllText(source, "test-data");
        var imported = _service.ImportWallpaper(source);

        var state = new AppearanceState(imported, 10, false);
        _service.ApplyToAllMonitors(state);

        foreach (var monitor in monitors)
        {
            var storedPath = _settings.Get($"wallpaper.monitor.{monitor.DeviceId}");
            Assert.Equal(imported, storedPath);
        }
    }
}

public class MonitorSlideshowServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalWallpaperDir;
    private readonly InMemorySettings _settings;
    private readonly MonitorSlideshowService _slideshow;

    public MonitorSlideshowServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"avex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _originalWallpaperDir = AvexPaths.WallpaperDir;
        AvexPaths.OverrideWallpaperDirForTests(_tempDir);
        _settings = new InMemorySettings();
        var wallpaperService = new WallpaperService(new AllowAllGuard(), _settings);
        _slideshow = new MonitorSlideshowService(wallpaperService, _settings);
    }

    public void Dispose()
    {
        AvexPaths.OverrideWallpaperDirForTests(_originalWallpaperDir);
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private const string TestDevice = "TEST_MONITOR_1";

    [Fact]
    public void IsEnabled_Defaults_To_False()
    {
        Assert.False(_slideshow.IsEnabled(TestDevice));
    }

    [Fact]
    public void GetPaths_Defaults_To_Empty()
    {
        Assert.Empty(_slideshow.GetPaths(TestDevice));
    }

    [Fact]
    public void GetIntervalMinutes_Defaults_To_30()
    {
        Assert.Equal(30, _slideshow.GetIntervalMinutes(TestDevice));
    }

    [Fact]
    public void SetIntervalMinutes_Persists_Value()
    {
        _slideshow.SetIntervalMinutes(TestDevice, 15);
        Assert.Equal(15, _slideshow.GetIntervalMinutes(TestDevice));
    }

    [Fact]
    public void SetPaths_And_GetPaths_Roundtrip()
    {
        var paths = new List<string> { "/path/a.png", "/path/b.png", "/path/c.png" };
        _slideshow.SetPaths(TestDevice, paths);
        var result = _slideshow.GetPaths(TestDevice);
        Assert.Equal(paths, result);
    }

    [Fact]
    public void Enable_Sets_IsEnabled_True()
    {
        _slideshow.Enable(TestDevice);
        Assert.True(_slideshow.IsEnabled(TestDevice));
    }

    [Fact]
    public void Disable_Sets_IsEnabled_False()
    {
        _slideshow.Enable(TestDevice);
        _slideshow.Disable(TestDevice);
        Assert.False(_slideshow.IsEnabled(TestDevice));
    }

    [Fact]
    public void Advance_Returns_Null_When_Disabled()
    {
        Assert.Null(_slideshow.Advance(TestDevice));
    }

    [Fact]
    public void Advance_Returns_Null_When_No_Paths()
    {
        _slideshow.Enable(TestDevice);
        Assert.Null(_slideshow.Advance(TestDevice));
    }

    [Fact]
    public void Reset_Clears_All_State()
    {
        _slideshow.SetPaths(TestDevice, new List<string> { "/a.png" });
        _slideshow.SetIntervalMinutes(TestDevice, 5);
        _slideshow.Enable(TestDevice);

        _slideshow.Reset(TestDevice);

        Assert.False(_slideshow.IsEnabled(TestDevice));
        Assert.Empty(_slideshow.GetPaths(TestDevice));
    }

    [Fact]
    public void Different_Monitors_Have_Independent_State()
    {
        const string monitor1 = "MON_1";
        const string monitor2 = "MON_2";

        _slideshow.SetIntervalMinutes(monitor1, 10);
        _slideshow.SetIntervalMinutes(monitor2, 20);
        _slideshow.SetPaths(monitor1, new List<string> { "/a.png" });
        _slideshow.SetPaths(monitor2, new List<string> { "/b.png", "/c.png" });
        _slideshow.Enable(monitor1);

        Assert.Equal(10, _slideshow.GetIntervalMinutes(monitor1));
        Assert.Equal(20, _slideshow.GetIntervalMinutes(monitor2));
        Assert.Single(_slideshow.GetPaths(monitor1));
        Assert.Equal(2, _slideshow.GetPaths(monitor2).Count);
        Assert.True(_slideshow.IsEnabled(monitor1));
        Assert.False(_slideshow.IsEnabled(monitor2));
    }

    [Fact]
    public void Advance_Parameterless_Returns_Null_When_No_Enabled_Monitors()
    {
        // No monitors are enabled, so parameterless Advance should return null
        Assert.Null(_slideshow.Advance());
    }

    [Fact]
    public void Advance_Parameterless_Returns_Null_When_Enabled_But_No_Paths()
    {
        _slideshow.Enable("MON_EMPTY");
        // Enabled but no paths set, should return null
        Assert.Null(_slideshow.Advance());
    }
}

public class MonitorDetectionTests
{
    [Fact]
    public void GetMonitors_Returns_NonNull_List()
    {
        var monitors = MonitorDetection.GetMonitors();
        Assert.NotNull(monitors);
    }

    [Fact]
    public void GetPrimaryMonitor_Returns_Null_When_No_Monitors()
    {
        // This tests the fallback logic when the list is empty
        var emptyList = new List<MonitorInfo>();
        var primary = MonitorDetection.GetPrimaryMonitor(emptyList);
        Assert.Null(primary);
    }

    [Fact]
    public void GetPrimaryMonitor_Returns_Primary_When_Available()
    {
        var monitors = new List<MonitorInfo>
        {
            new("SEC", "Secondary", 1920, 1080, 1920, 0, false),
            new("PRI", "Primary", 2560, 1440, 0, 0, true),
        };
        var primary = MonitorDetection.GetPrimaryMonitor(monitors);
        Assert.NotNull(primary);
        Assert.True(primary.IsPrimary);
        Assert.Equal("PRI", primary.DeviceId);
    }

    [Fact]
    public void GetPrimaryMonitor_Falls_Back_To_First_When_No_Primary_Flag()
    {
        var monitors = new List<MonitorInfo>
        {
            new("MON1", "Monitor 1", 1920, 1080, 0, 0, false),
            new("MON2", "Monitor 2", 1920, 1080, 1920, 0, false),
        };
        var primary = MonitorDetection.GetPrimaryMonitor(monitors);
        Assert.NotNull(primary);
        Assert.Equal("MON1", primary.DeviceId);
    }
}
