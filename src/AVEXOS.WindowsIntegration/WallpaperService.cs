using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using AVEXOS.Core;
using AVEXOS.Security;

namespace AVEXOS.WindowsIntegration;

public sealed class WallpaperService : IWallpaperService
{
    private readonly IFileSystemGuard _guard;

    public WallpaperService(IFileSystemGuard guard) => _guard = guard;

    public AppearanceState Read()
    {
        var sb = new StringBuilder(32_768);
        NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETDESKWALLPAPER, (uint)sb.Capacity, sb, 0);
        var wallpaper = sb.ToString();

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        var style = int.TryParse(key?.GetValue("WallpaperStyle") as string, out var s) ? s : 10;
        var tile = (key?.GetValue("TileWallpaper") as string) == "1";

        return new AppearanceState(wallpaper, style, tile);
    }

    public void Apply(AppearanceState state)
    {
        if (!RegistryPolicy.IsWritable(@"HKEY_CURRENT_USER\Control Panel\Desktop", out var reason))
        {
            throw new UnauthorizedAccessException($"Registry blocked: {reason}");
        }

        using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop", writable: true);
        key.SetValue("WallpaperStyle", state.WallpaperStyle.ToString(System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
        key.SetValue("TileWallpaper", state.TileWallpaper ? "1" : "0", RegistryValueKind.String);

        var target = string.IsNullOrWhiteSpace(state.WallpaperPath) ? "" : state.WallpaperPath;
        if (!NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETDESKWALLPAPER, 0, target, NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public string ImportWallpaper(string sourcePath)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Source wallpaper not found.", sourcePath);
        var ext = Path.GetExtension(sourcePath);
        var dest = Path.Combine(AvexPaths.WallpaperDir, $"avex-wallpaper{ext}");
        var guard = _guard.EnsureWritable(dest);
        if (!guard.Allowed) throw new UnauthorizedAccessException($"Blocked by path guard: {guard.Reason}");
        Directory.CreateDirectory(AvexPaths.WallpaperDir);
        File.Copy(sourcePath, guard.CanonicalPath, overwrite: true);
        return guard.CanonicalPath;
    }
}
