using AVEXOS.Core;
using Xunit;

namespace AVEXOS.Core.Tests;

public class SystemInfoServiceTests
{
    [Fact]
    public void WindowsVersion_Returns_NonEmpty()
    {
        var service = CreateService();
        Assert.False(string.IsNullOrWhiteSpace(service.WindowsVersion));
    }

    [Fact]
    public void WindowsBuild_Contains_Build_Prefix()
    {
        var service = CreateService();
        Assert.StartsWith("Build ", service.WindowsBuild);
    }

    [Fact]
    public void DotNetVersion_Returns_NonEmpty()
    {
        var service = CreateService();
        Assert.False(string.IsNullOrWhiteSpace(service.DotNetVersion));
    }

    [Fact]
    public void AppVersion_Returns_Valid_Version()
    {
        var service = CreateService();
        Assert.False(string.IsNullOrWhiteSpace(service.AppVersion));
    }

    [Fact]
    public void CurrentThemeId_Returns_Default_When_Not_Set()
    {
        var settings = new InMemorySettingsStore();
        var service = new SystemInfoService(settings);
        Assert.Equal("avex.dark", service.CurrentThemeId);
    }

    [Fact]
    public void CurrentThemeId_Returns_Stored_Value()
    {
        var settings = new InMemorySettingsStore();
        settings.Set("theme.id", "avex.light");
        var service = new SystemInfoService(settings);
        Assert.Equal("avex.light", service.CurrentThemeId);
    }

    [Fact]
    public void CurrentThemeMode_Returns_Default_When_Not_Set()
    {
        var settings = new InMemorySettingsStore();
        var service = new SystemInfoService(settings);
        Assert.Equal("dark", service.CurrentThemeMode);
    }

    [Fact]
    public void CurrentThemeMode_Returns_Stored_Value()
    {
        var settings = new InMemorySettingsStore();
        settings.Set("theme.mode", "light");
        var service = new SystemInfoService(settings);
        Assert.Equal("light", service.CurrentThemeMode);
    }

    [Fact]
    public void SessionDuration_Is_Positive()
    {
        var service = CreateService();
        Assert.True(service.SessionDuration >= TimeSpan.Zero);
    }

    [Fact]
    public void SessionDurationDisplay_Returns_NonEmpty()
    {
        var service = CreateService();
        Assert.False(string.IsNullOrWhiteSpace(service.SessionDurationDisplay));
    }

    [Fact]
    public void MachineName_Returns_NonEmpty()
    {
        var service = CreateService();
        Assert.False(string.IsNullOrWhiteSpace(service.MachineName));
    }

    [Fact]
    public void UserName_Returns_NonEmpty()
    {
        var service = CreateService();
        Assert.False(string.IsNullOrWhiteSpace(service.UserName));
    }

    [Fact]
    public void ProcessorCount_Is_Positive()
    {
        var service = CreateService();
        Assert.True(service.ProcessorCount > 0);
    }

    [Fact]
    public void TotalMemoryMb_Is_Positive()
    {
        var service = CreateService();
        Assert.True(service.TotalMemoryMb > 0);
    }

    [Fact]
    public void ActiveWallpaperPath_Returns_Default_When_No_Slideshow()
    {
        var settings = new InMemorySettingsStore();
        var service = new SystemInfoService(settings);
        Assert.Equal("(Default)", service.ActiveWallpaperPath);
    }

    [Fact]
    public void ActiveWallpaperPath_Returns_First_Slideshow_Path()
    {
        var settings = new InMemorySettingsStore();
        settings.Set("wallpaper.slideshow.paths", @"C:\wall1.png|C:\wall2.png");
        var service = new SystemInfoService(settings);
        Assert.Equal(@"C:\wall1.png", service.ActiveWallpaperPath);
    }

    private static SystemInfoService CreateService()
        => new(new InMemorySettingsStore());
}

file class InMemorySettingsStore : ISettingsStore
{
    private readonly Dictionary<string, string> _data = new();

    public string? Get(string key) => _data.TryGetValue(key, out var v) ? v : null;
    public void Set(string key, string value) => _data[key] = value;
    public IReadOnlyDictionary<string, string> GetAll() => _data;
    public void SetMany(IReadOnlyDictionary<string, string> values)
    {
        foreach (var (k, v) in values) _data[k] = v;
    }
}
