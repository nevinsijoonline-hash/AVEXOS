using System.Text.Json;
using AVEXOS.Core;
using Xunit;

namespace AVEXOS.Core.Tests;

public class WorkspaceProfileServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly IWallpaperService _wallpaper;
    private readonly IThemeService _theme;
    private readonly WorkspaceProfileService _sut;

    public WorkspaceProfileServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"avex-profile-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        AvexPaths.OverrideProfilesDirForTests(_testDir);

        _wallpaper = new FakeWallpaperService();
        _theme = new FakeThemeService();
        _sut = new WorkspaceProfileService(_wallpaper, _theme);
    }

    // --- GetAll ---

    [Fact]
    public void GetAll_ReturnsEmpty_WhenNoProfiles()
    {
        var result = _sut.GetAll();
        Assert.Empty(result);
    }

    [Fact]
    public void GetAll_ReturnsSavedProfiles()
    {
        _sut.Save(MakeProfile("p1", "Work"));
        _sut.Save(MakeProfile("p2", "Gaming"));

        var result = _sut.GetAll();
        Assert.Equal(2, result.Count);
    }

    // --- Get ---

    [Fact]
    public void Get_ReturnsNull_ForUnknownId()
    {
        var result = _sut.Get("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public void Get_ReturnsProfile_ById()
    {
        var profile = MakeProfile("p1", "Work");
        _sut.Save(profile);

        var result = _sut.Get("p1");
        Assert.NotNull(result);
        Assert.Equal("Work", result!.Name);
    }

    // --- Save ---

    [Fact]
    public void Save_PersistsProfileToDisk()
    {
        var profile = MakeProfile("p1", "Creative");
        _sut.Save(profile);

        var filePath = Path.Combine(_testDir, "p1.json");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void Save_ThrowsOnEmptyName()
    {
        var profile = MakeProfile("p1", "");
        Assert.Throws<ArgumentException>(() => _sut.Save(profile));
    }

    [Fact]
    public void Save_ThrowsOnNullName()
    {
        var profile = MakeProfile("p1", null!);
        Assert.ThrowsAny<ArgumentException>(() => _sut.Save(profile));
    }

    [Fact]
    public void Save_ThrowsOnEmptyId()
    {
        var profile = MakeProfile("", "Work");
        Assert.Throws<ArgumentException>(() => _sut.Save(profile));
    }

    [Fact]
    public void Save_ThrowsOnEmptyWallpaperPath()
    {
        var profile = new WorkspaceProfile("p1", "Work", "", "theme1", "dark", DateTimeOffset.UtcNow, false);
        Assert.Throws<ArgumentException>(() => _sut.Save(profile));
    }

    [Fact]
    public void Save_ThrowsOnEmptyThemeId()
    {
        var profile = new WorkspaceProfile("p1", "Work", @"C:\wp.png", "", "dark", DateTimeOffset.UtcNow, false);
        Assert.Throws<ArgumentException>(() => _sut.Save(profile));
    }

    [Fact]
    public void Save_ThrowsOnEmptyThemeMode()
    {
        var profile = new WorkspaceProfile("p1", "Work", @"C:\wp.png", "theme1", "", DateTimeOffset.UtcNow, false);
        Assert.Throws<ArgumentException>(() => _sut.Save(profile));
    }

    [Fact]
    public void Save_OverwritesExistingProfile()
    {
        _sut.Save(MakeProfile("p1", "Work"));
        _sut.Save(MakeProfile("p1", "Updated Work"));

        var result = _sut.Get("p1");
        Assert.Equal("Updated Work", result!.Name);
    }

    // --- Delete ---

    [Fact]
    public void Delete_RemovesProfileFromDisk()
    {
        _sut.Save(MakeProfile("p1", "Work"));
        _sut.Delete("p1");

        Assert.Null(_sut.Get("p1"));
        Assert.False(File.Exists(Path.Combine(_testDir, "p1.json")));
    }

    [Fact]
    public void Delete_NoOp_WhenIdNotFound()
    {
        // Should not throw
        _sut.Delete("nonexistent");
    }

    [Fact]
    public void Delete_ClearsDefault_WhenDeletingDefaultProfile()
    {
        var profile = MakeProfile("p1", "Work", isDefault: true);
        _sut.Save(profile);
        _sut.Delete("p1");

        var all = _sut.GetAll();
        Assert.DoesNotContain(all, p => p.IsDefault);
    }

    // --- SetDefault ---

    [Fact]
    public void SetDefault_MarksProfileAsDefault()
    {
        _sut.Save(MakeProfile("p1", "Work"));
        _sut.SetDefault("p1");

        var result = _sut.Get("p1");
        Assert.True(result!.IsDefault);
    }

    [Fact]
    public void SetDefault_ClearsOtherDefaults()
    {
        _sut.Save(MakeProfile("p1", "Work", isDefault: true));
        _sut.Save(MakeProfile("p2", "Gaming"));

        _sut.SetDefault("p2");

        var p1 = _sut.Get("p1");
        var p2 = _sut.Get("p2");
        Assert.False(p1!.IsDefault);
        Assert.True(p2!.IsDefault);
    }

    [Fact]
    public void SetDefault_ThrowsOnUnknownId()
    {
        Assert.Throws<KeyNotFoundException>(() => _sut.SetDefault("nonexistent"));
    }

    // --- Apply ---

    [Fact]
    public void Apply_AppliesWallpaperToAllMonitors()
    {
        var profile = MakeProfile("p1", "Work");
        _sut.Save(profile);

        _sut.Apply("p1");

        var fake = (FakeWallpaperService)_wallpaper;
        Assert.Equal(1, fake.ApplyToAllMonitorsCallCount);
        Assert.Equal(profile.WallpaperPath, fake.LastAppliedState!.WallpaperPath);
    }

    [Fact]
    public void Apply_AppliesTheme()
    {
        var profile = MakeProfile("p1", "Work");
        _sut.Save(profile);

        _sut.Apply("p1");

        var fake = (FakeThemeService)_theme;
        Assert.Equal(1, fake.ApplyCallCount);
        Assert.Equal(profile.ThemeId, fake.LastAppliedThemeId);
        Assert.Equal(profile.ThemeMode, fake.LastAppliedMode);
    }

    [Fact]
    public void Apply_ThrowsOnUnknownId()
    {
        Assert.Throws<KeyNotFoundException>(() => _sut.Apply("nonexistent"));
    }

    // --- Persistence round-trip ---

    [Fact]
    public void ProfilesSurviveServiceRecreation()
    {
        _sut.Save(MakeProfile("p1", "Work"));
        _sut.Save(MakeProfile("p2", "Gaming", isDefault: true));

        // Create a new service instance (simulates app restart)
        var sut2 = new WorkspaceProfileService(_wallpaper, _theme);

        var all = sut2.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, p => p.Name == "Work");
        Assert.Contains(all, p => p.Name == "Gaming" && p.IsDefault);
    }

    // --- Helpers ---

    private static WorkspaceProfile MakeProfile(string id, string name, bool isDefault = false) =>
        new(id, name, @"C:\wallpapers\test.png", "avex.dark", "dark", DateTimeOffset.UtcNow, isDefault);

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }
}

// --- Fakes ---

file class FakeWallpaperService : IWallpaperService
{
    public int ApplyToAllMonitorsCallCount { get; private set; }
    public AppearanceState? LastAppliedState { get; private set; }

    public AppearanceState Read() => new(@"C:\current.png", 10, false);
    public void Apply(AppearanceState state) { }
    public string ImportWallpaper(string sourcePath) => sourcePath;
    public IReadOnlyList<MonitorInfo> GetMonitors() => new List<MonitorInfo>
    {
        new("mon1", "Monitor 1", 1920, 1080, 0, 0, true)
    };
    public MonitorWallpaperState? ReadMonitor(string deviceId) => null;
    public void ApplyToMonitor(string deviceId, AppearanceState state) { }
    public void ApplyToAllMonitors(AppearanceState state)
    {
        ApplyToAllMonitorsCallCount++;
        LastAppliedState = state;
    }
}

file class FakeThemeService : IThemeService
{
    public int ApplyCallCount { get; private set; }
    public string? LastAppliedThemeId { get; private set; }
    public string? LastAppliedMode { get; private set; }
    public string CurrentThemeId => LastAppliedThemeId ?? "avex.dark";
    public string CurrentMode => LastAppliedMode ?? "dark";

    public void Apply(string themeId, string mode)
    {
        ApplyCallCount++;
        LastAppliedThemeId = themeId;
        LastAppliedMode = mode;
    }
}
