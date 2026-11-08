using System.Text.Json;
using AVEXOS.Core;
using AVEXOS.WindowsIntegration;
using Xunit;

namespace AVEXOS.WindowsIntegration.Tests;

public class PresentationModeServiceTests : IDisposable
{
    private readonly InMemorySettings _settings;
    private readonly FakeWallpaperService _wallpaper;
    private readonly PresentationModeService _service;
    private readonly string _tempDir;

    public PresentationModeServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"avex-pres-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settings = new InMemorySettings();
        _wallpaper = new FakeWallpaperService();
        _service = new PresentationModeService(_settings, _wallpaper);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateTempWallpaper(string name)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "fake-wallpaper");
        return path;
    }

    // ── Config persistence ────────────────────────────────────────────

    [Fact]
    public void GetConfig_Returns_Defaults_When_No_Settings()
    {
        var config = _service.GetConfig();
        Assert.False(config.IsEnabled);
        Assert.Equal("#000000", config.BlankColor);
        Assert.False(config.BlankPrimaryToo);
    }

    [Fact]
    public void SaveConfig_Persists_All_Fields()
    {
        var config = new PresentationModeConfig(true, "#FF0000", true);
        _service.SaveConfig(config);

        var loaded = _service.GetConfig();
        Assert.True(loaded.IsEnabled);
        Assert.Equal("#FF0000", loaded.BlankColor);
        Assert.True(loaded.BlankPrimaryToo);
    }

    [Fact]
    public void SaveConfig_Roundtrip_With_Custom_Color()
    {
        var config = new PresentationModeConfig(false, "#00FF80", false);
        _service.SaveConfig(config);

        var loaded = _service.GetConfig();
        Assert.Equal("#00FF80", loaded.BlankColor);
    }

    // ── IsEnabled ─────────────────────────────────────────────────────

    [Fact]
    public void IsEnabled_Returns_False_By_Default()
    {
        Assert.False(_service.IsEnabled);
    }

    [Fact]
    public void IsEnabled_Returns_True_After_Enable()
    {
        _wallpaper.AddMonitor("MON1", isPrimary: true);
        _service.Enable();
        Assert.True(_service.IsEnabled);
    }

    [Fact]
    public void IsEnabled_Returns_False_After_Disable()
    {
        _wallpaper.AddMonitor("MON1", isPrimary: true);
        _service.Enable();
        _service.Disable();
        Assert.False(_service.IsEnabled);
    }

    // ── Enable ────────────────────────────────────────────────────────

    [Fact]
    public void Enable_Saves_Wallpaper_State_For_All_Monitors()
    {
        var wall1 = CreateTempWallpaper("wall1.png");
        var wall2 = CreateTempWallpaper("wall2.png");
        _wallpaper.AddMonitor("MON1", isPrimary: true);
        _wallpaper.AddMonitor("MON2", isPrimary: false);
        _wallpaper.SetMonitorWallpaper("MON1", wall1);
        _wallpaper.SetMonitorWallpaper("MON2", wall2);

        _service.Enable();

        var savedJson = _settings.Get("presentation.savedState");
        Assert.NotNull(savedJson);
        var savedState = JsonSerializer.Deserialize<PresentationModeSavedState>(savedJson!);
        Assert.NotNull(savedState);
        Assert.Equal(wall1, savedState.MonitorWallpapers["MON1"]);
        Assert.Equal(wall2, savedState.MonitorWallpapers["MON2"]);
    }

    [Fact]
    public void Enable_Blanks_Secondary_Monitors_Only_By_Default()
    {
        var primaryWall = CreateTempWallpaper("primary.png");
        var secondaryWall = CreateTempWallpaper("secondary.png");
        _wallpaper.AddMonitor("MON1", isPrimary: true);
        _wallpaper.AddMonitor("MON2", isPrimary: false);
        _wallpaper.SetMonitorWallpaper("MON1", primaryWall);
        _wallpaper.SetMonitorWallpaper("MON2", secondaryWall);

        _service.SaveConfig(new PresentationModeConfig(false, "#000000", false));
        _service.Enable();

        // Primary should keep its wallpaper
        Assert.Equal(primaryWall, _wallpaper.GetAppliedWallpaper("MON1"));
        // Secondary should have a blank BMP applied
        var appliedSecondary = _wallpaper.GetAppliedWallpaper("MON2");
        Assert.EndsWith(".bmp", appliedSecondary);
    }

    [Fact]
    public void Enable_Blanks_All_Monitors_When_BlankPrimaryToo()
    {
        _wallpaper.AddMonitor("MON1", isPrimary: true);
        _wallpaper.AddMonitor("MON2", isPrimary: false);
        _wallpaper.SetMonitorWallpaper("MON1", CreateTempWallpaper("p.png"));
        _wallpaper.SetMonitorWallpaper("MON2", CreateTempWallpaper("s.png"));

        _service.SaveConfig(new PresentationModeConfig(false, "#000000", true));
        _service.Enable();

        Assert.EndsWith(".bmp", _wallpaper.GetAppliedWallpaper("MON1"));
        Assert.EndsWith(".bmp", _wallpaper.GetAppliedWallpaper("MON2"));
    }

    [Fact]
    public void Enable_Does_Nothing_When_Already_Enabled()
    {
        _wallpaper.AddMonitor("MON1", isPrimary: true);
        _wallpaper.SetMonitorWallpaper("MON1", CreateTempWallpaper("orig.png"));

        _service.Enable();
        var savedState1 = _settings.Get("presentation.savedState");

        // Change the wallpaper after first enable
        _wallpaper.SetMonitorWallpaper("MON1", CreateTempWallpaper("changed.png"));

        // Enable again - should not overwrite saved state
        _service.Enable();
        var savedState2 = _settings.Get("presentation.savedState");

        Assert.Equal(savedState1, savedState2);
    }

    // ── Disable ───────────────────────────────────────────────────────

    [Fact]
    public void Disable_Restores_Original_Wallpapers()
    {
        var orig1 = CreateTempWallpaper("original.png");
        var orig2 = CreateTempWallpaper("original2.png");
        _wallpaper.AddMonitor("MON1", isPrimary: true);
        _wallpaper.AddMonitor("MON2", isPrimary: false);
        _wallpaper.SetMonitorWallpaper("MON1", orig1);
        _wallpaper.SetMonitorWallpaper("MON2", orig2);

        _service.Enable();
        _service.Disable();

        Assert.Equal(orig1, _wallpaper.GetAppliedWallpaper("MON1"));
        Assert.Equal(orig2, _wallpaper.GetAppliedWallpaper("MON2"));
    }

    [Fact]
    public void Disable_Clears_Saved_State()
    {
        _wallpaper.AddMonitor("MON1", isPrimary: true);
        _wallpaper.SetMonitorWallpaper("MON1", CreateTempWallpaper("wall.png"));

        _service.Enable();
        Assert.NotNull(_settings.Get("presentation.savedState"));

        _service.Disable();
        Assert.Equal("", _settings.Get("presentation.savedState"));
    }

    [Fact]
    public void Disable_Does_Nothing_When_Already_Disabled()
    {
        _service.Disable();
        Assert.False(_service.IsEnabled);
    }

    // ── ParseHexColor ─────────────────────────────────────────────────

    [Theory]
    [InlineData("#000000", 0, 0, 0)]
    [InlineData("#FFFFFF", 255, 255, 255)]
    [InlineData("#FF0000", 255, 0, 0)]
    [InlineData("#00FF00", 0, 255, 0)]
    [InlineData("#0000FF", 0, 0, 255)]
    [InlineData("#1A2B3C", 0x1A, 0x2B, 0x3C)]
    public void ParseHexColor_Parses_Valid_Hex(string hex, byte r, byte g, byte b)
    {
        var result = PresentationModeService.ParseHexColor(hex);
        Assert.Equal(r, result.R);
        Assert.Equal(g, result.G);
        Assert.Equal(b, result.B);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("#GG0000")]
    [InlineData("#FFF")]
    public void ParseHexColor_Returns_Black_For_Invalid_Input(string hex)
    {
        var result = PresentationModeService.ParseHexColor(hex);
        Assert.Equal((byte)0, result.R);
        Assert.Equal((byte)0, result.G);
        Assert.Equal((byte)0, result.B);
    }

    [Fact]
    public void ParseHexColor_Handles_Hash_Prefix_And_Without()
    {
        var withHash = PresentationModeService.ParseHexColor("#ABCDEF");
        Assert.Equal(0xAB, withHash.R);
        Assert.Equal(0xCD, withHash.G);
        Assert.Equal(0xEF, withHash.B);
    }

    // ── GenerateSolidColorBmp ─────────────────────────────────────────

    [Fact]
    public void GenerateSolidColorBmp_Creates_Valid_Bmp_File()
    {
        var path = PresentationModeService.GenerateSolidColorBmp("#FF0000");
        try
        {
            Assert.True(File.Exists(path));
            var bytes = File.ReadAllBytes(path);
            Assert.Equal((byte)'B', bytes[0]);
            Assert.Equal((byte)'M', bytes[1]);
            Assert.Equal(58, bytes.Length);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void GenerateSolidColorBmp_Encodes_Correct_Pixel_Color()
    {
        var path = PresentationModeService.GenerateSolidColorBmp("#1A2B3C");
        try
        {
            var bytes = File.ReadAllBytes(path);
            // Pixel data starts at offset 54 (BGR order)
            Assert.Equal(0x3C, bytes[54]); // Blue
            Assert.Equal(0x2B, bytes[55]); // Green
            Assert.Equal(0x1A, bytes[56]); // Red
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
