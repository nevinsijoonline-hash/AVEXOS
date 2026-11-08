namespace AVEXOS.Core;

/// <summary>
/// AVEX OS data locations. Nothing is ever written outside these roots
/// or an explicitly opted-in user folder.
/// </summary>
public static class AvexPaths
{
    private static string? _backupDirOverride;
    private static string? _wallpaperDirOverride;
    private static string? _themeDirOverride;

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVEXOS");

    public static string RoamingRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AVEXOS");

    public static string BackupDir => _backupDirOverride ?? Path.Combine(Root, "backups");
    public static string LogDir => Path.Combine(Root, "logs");
    public static string CacheDir => Path.Combine(Root, "cache");
    public static string ThemeDir => _themeDirOverride ?? Path.Combine(Root, "themes");
    public static string UserThemeDir => Path.Combine(ThemeDir, "user");
    public static string WallpaperDir => _wallpaperDirOverride ?? Path.Combine(Root, "wallpapers");
    public static string DatabasePath => Path.Combine(Root, "avex.db");

    public static void EnsureCreated()
    {
        foreach (var dir in new[] { Root, RoamingRoot, BackupDir, LogDir, CacheDir, ThemeDir, UserThemeDir, WallpaperDir })
        {
            Directory.CreateDirectory(dir);
        }
    }

    /// <summary>Test-only: override the backup directory. Do not call from production code.</summary>
    public static void OverrideBackupDirForTests(string path)
    {
        _backupDirOverride = path;
    }

    /// <summary>Test-only: override the wallpaper directory. Do not call from production code.</summary>
    public static void OverrideWallpaperDirForTests(string path)
    {
        _wallpaperDirOverride = path;
    }

    /// <summary>Test-only: override the theme directory. Do not call from production code.</summary>
    public static void OverrideThemeDirForTests(string path)
    {
        _themeDirOverride = path;
    }
}
