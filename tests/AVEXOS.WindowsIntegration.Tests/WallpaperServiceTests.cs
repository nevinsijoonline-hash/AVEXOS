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
        var service = new WallpaperService(guard);

        var source = Path.Combine(_tempDir, "source.png");
        File.WriteAllText(source, "fake-image-data");

        var result = service.ImportWallpaper(source);

        Assert.True(File.Exists(result));
        Assert.Equal("avex-wallpaper.png", Path.GetFileName(result));
        Assert.Equal("fake-image-data", File.ReadAllText(result));
    }

    [Fact]
    public void ImportWallpaper_Throws_When_Source_Not_Found()
    {
        var guard = new AllowAllGuard();
        var service = new WallpaperService(guard);

        Assert.Throws<FileNotFoundException>(() => service.ImportWallpaper(@"C:\nonexistent\file.png"));
    }

    [Fact]
    public void ImportWallpaper_Throws_When_Guard_Denies()
    {
        var guard = new DenyAllGuard();
        var service = new WallpaperService(guard);

        var source = Path.Combine(_tempDir, "source.png");
        File.WriteAllText(source, "fake-image-data");

        var ex = Assert.Throws<UnauthorizedAccessException>(() => service.ImportWallpaper(source));
        Assert.Contains("Blocked by path guard", ex.Message);
    }

    [Fact]
    public void ImportWallpaper_Overwrites_Existing_File()
    {
        var guard = new AllowAllGuard();
        var service = new WallpaperService(guard);

        var source = Path.Combine(_tempDir, "source.png");
        File.WriteAllText(source, "first-version");
        service.ImportWallpaper(source);

        File.WriteAllText(source, "second-version");
        var result = service.ImportWallpaper(source);

        Assert.Equal("second-version", File.ReadAllText(result));
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
