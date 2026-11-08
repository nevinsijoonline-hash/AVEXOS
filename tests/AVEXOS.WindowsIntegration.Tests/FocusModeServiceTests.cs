using System.Text.Json;
using AVEXOS.Core;
using AVEXOS.WindowsIntegration;
using Xunit;

namespace AVEXOS.WindowsIntegration.Tests;

public class FocusModeServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly InMemorySettings _settings;
    private readonly FakeWallpaperService _wallpaper;
    private readonly FocusModeService _service;

    public FocusModeServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"avex-focus-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settings = new InMemorySettings();
        _wallpaper = new FakeWallpaperService();
        _service = new FocusModeService(_settings, _wallpaper);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void GetConfig_Returns_Default_When_Nothing_Saved()
    {
        var config = _service.GetConfig();
        Assert.False(config.IsEnabled);
        Assert.True(config.DimNonPrimary);
        Assert.Equal(50, config.DimLevel);
        Assert.False(config.UseSolidColor);
        Assert.Equal("#000000", config.SolidColor);
        Assert.False(config.BlurWallpaper);
        Assert.Equal(10, config.BlurRadius);
    }

    [Fact]
    public void SaveConfig_Persists_To_SettingsStore()
    {
        var config = new FocusModeConfig
        {
            IsEnabled = true, DimLevel = 75, SolidColor = "#FF0000",
            UseSolidColor = true, BlurWallpaper = true, BlurRadius = 20,
        };
        _service.SaveConfig(config);
        var service2 = new FocusModeService(_settings, _wallpaper);
        var loaded = service2.GetConfig();
        Assert.True(loaded.IsEnabled);
        Assert.Equal(75, loaded.DimLevel);
        Assert.Equal("#FF0000", loaded.SolidColor);
        Assert.True(loaded.UseSolidColor);
        Assert.True(loaded.BlurWallpaper);
        Assert.Equal(20, loaded.BlurRadius);
    }

    [Fact]
    public void SaveConfig_Roundtrip_Preserves_All_Fields()
    {
        var original = new FocusModeConfig
        {
            IsEnabled = false, DimNonPrimary = false, DimLevel = 30,
            UseSolidColor = true, SolidColor = "#AABBCC", BlurWallpaper = true, BlurRadius = 5,
        };
        _service.SaveConfig(original);
        var restored = _service.GetConfig();
        Assert.Equal(original.IsEnabled, restored.IsEnabled);
        Assert.Equal(original.DimNonPrimary, restored.DimNonPrimary);
        Assert.Equal(original.DimLevel, restored.DimLevel);
        Assert.Equal(original.UseSolidColor, restored.UseSolidColor);
        Assert.Equal(original.SolidColor, restored.SolidColor);
        Assert.Equal(original.BlurWallpaper, restored.BlurWallpaper);
        Assert.Equal(original.BlurRadius, restored.BlurRadius);
    }

    [Fact]
    public void IsEnabled_Defaults_To_False() => Assert.False(_service.IsEnabled);

    [Fact]
    public void Enable_Sets_IsEnabled_True()
    {
        _service.Enable();
        Assert.True(_service.IsEnabled);
    }

    [Fact]
    public void Disable_Sets_IsEnabled_False()
    {
        _service.Enable();
        _service.Disable();
        Assert.False(_service.IsEnabled);
    }

    [Fact]
    public void Enable_Saves_Original_Wallpaper_State()
    {
        _wallpaper.AddMonitor("MON1", false);
        _wallpaper.AddMonitor("MON2", false);
        _wallpaper.SetMonitorWallpaper("MON1", @"C:\wall1.png");
        _wallpaper.SetMonitorWallpaper("MON2", @"C:\wall2.png");
        _service.Enable();
        var savedJson = _settings.Get("focusmode.saved_state");
        Assert.NotNull(savedJson);
        Assert.NotEmpty(savedJson);
        var saved = JsonSerializer.Deserialize<FocusModeSavedState>(savedJson);
        Assert.NotNull(saved);
        Assert.True(saved.MonitorWallpapers.ContainsKey("MON1"));
        Assert.True(saved.MonitorWallpapers.ContainsKey("MON2"));
    }

    [Fact]
    public void Enable_Dims_NonPrimary_Monitors_With_SolidColor()
    {
        var config = new FocusModeConfig
        {
            IsEnabled = true, DimNonPrimary = true, UseSolidColor = true,
            SolidColor = "#000000", DimLevel = 80,
        };
        _service.SaveConfig(config);
        _wallpaper.AddMonitor("MON_PRIMARY", true);
        _wallpaper.AddMonitor("MON_SECONDARY", false);
        _wallpaper.SetMonitorWallpaper("MON_PRIMARY", @"C:\primary.png");
        _wallpaper.SetMonitorWallpaper("MON_SECONDARY", @"C:\secondary.png");
        _service.Enable();
        var primaryWp = _wallpaper.GetAppliedWallpaper("MON_PRIMARY");
        Assert.Contains("primary", primaryWp ?? "");
        var secondaryWp = _wallpaper.GetAppliedWallpaper("MON_SECONDARY");
        Assert.DoesNotContain("secondary", secondaryWp ?? "");
    }

    [Fact]
    public void Disable_Restores_Original_Wallpapers()
    {
        _wallpaper.AddMonitor("MON_PRIMARY", true);
        _wallpaper.AddMonitor("MON_SECONDARY", false);
        _wallpaper.SetMonitorWallpaper("MON_PRIMARY", @"C:\primary.png");
        _wallpaper.SetMonitorWallpaper("MON_SECONDARY", @"C:\secondary.png");
        var origP = _wallpaper.GetAppliedWallpaper("MON_PRIMARY");
        var origS = _wallpaper.GetAppliedWallpaper("MON_SECONDARY");
        _service.Enable();
        _service.Disable();
        Assert.Equal(origP, _wallpaper.GetAppliedWallpaper("MON_PRIMARY"));
        Assert.Equal(origS, _wallpaper.GetAppliedWallpaper("MON_SECONDARY"));
    }

    [Fact]
    public void Enable_With_DimNonPrimary_False_Does_Not_Dim_Secondary()
    {
        var config = new FocusModeConfig { IsEnabled = true, DimNonPrimary = false };
        _service.SaveConfig(config);
        _wallpaper.AddMonitor("MON_PRIMARY", true);
        _wallpaper.AddMonitor("MON_SECONDARY", false);
        _wallpaper.SetMonitorWallpaper("MON_PRIMARY", @"C:\primary.png");
        _wallpaper.SetMonitorWallpaper("MON_SECONDARY", @"C:\secondary.png");
        var origS = _wallpaper.GetAppliedWallpaper("MON_SECONDARY");
        _service.Enable();
        Assert.Equal(origS, _wallpaper.GetAppliedWallpaper("MON_SECONDARY"));
    }

    [Fact]
    public void Enable_Is_Idempotent_When_Already_Enabled()
    {
        _wallpaper.AddMonitor("MON_PRIMARY", true);
        _wallpaper.AddMonitor("MON_SECONDARY", false);
        _wallpaper.SetMonitorWallpaper("MON_PRIMARY", @"C:\primary.png");
        _wallpaper.SetMonitorWallpaper("MON_SECONDARY", @"C:\secondary.png");
        _service.Enable();
        var after1 = _wallpaper.GetAppliedWallpaper("MON_SECONDARY");
        _service.Enable();
        Assert.Equal(after1, _wallpaper.GetAppliedWallpaper("MON_SECONDARY"));
    }

    [Fact]
    public void Disable_When_Not_Enabled_Does_Not_Throw()
    {
        _service.Disable();
        Assert.False(_service.IsEnabled);
    }

    [Fact]
    public void Enable_With_Single_Monitor_Does_Not_Dim_Anything()
    {
        var config = new FocusModeConfig
        {
            IsEnabled = true, DimNonPrimary = true, UseSolidColor = true, SolidColor = "#000000"
        };
        _service.SaveConfig(config);
        _wallpaper.AddMonitor("MON_ONLY", true);
        _wallpaper.SetMonitorWallpaper("MON_ONLY", @"C:\only.png");
        var orig = _wallpaper.GetAppliedWallpaper("MON_ONLY");
        _service.Enable();
        Assert.Equal(orig, _wallpaper.GetAppliedWallpaper("MON_ONLY"));
    }

    [Fact]
    public void SaveConfig_Updates_IsEnabled_State()
    {
        _service.SaveConfig(new FocusModeConfig { IsEnabled = true });
        Assert.True(_service.GetConfig().IsEnabled);
    }

    [Fact]
    public void Enable_Persists_IsEnabled_True_In_Config()
    {
        _service.Enable();
        Assert.True(_service.GetConfig().IsEnabled);
    }

    [Fact]
    public void Disable_Persists_IsEnabled_False_In_Config()
    {
        _service.Enable();
        _service.Disable();
        Assert.False(_service.GetConfig().IsEnabled);
    }

    [Fact]
    public void Validate_Clamps_DimLevel()
    {
        var c = new FocusModeConfig { DimLevel = 150 };
        c.Validate();
        Assert.Equal(100, c.DimLevel);
        c.DimLevel = -10;
        c.Validate();
        Assert.Equal(0, c.DimLevel);
    }

    [Fact]
    public void Validate_Clamps_BlurRadius()
    {
        var c = new FocusModeConfig { BlurRadius = 200 };
        c.Validate();
        Assert.Equal(100, c.BlurRadius);
        c.BlurRadius = -5;
        c.Validate();
        Assert.Equal(0, c.BlurRadius);
    }

    [Fact]
    public void Validate_Fixes_Empty_SolidColor()
    {
        var c = new FocusModeConfig { SolidColor = "" };
        c.Validate();
        Assert.Equal("#000000", c.SolidColor);
    }
}
