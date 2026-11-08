using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using AVEXOS.Core;
using AVEXOS.ThemeEngine;
using AVEXOS.WindowsIntegration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AVEXOS.App;

public sealed partial class QuickActionsPage : Page
{
    public QuickActionsPage()
    {
        InitializeComponent();
        Loaded += (s, e) => RefreshState();
    }

    private void RefreshState()
    {
        try
        {
            var themeService = App.Services.GetRequiredService<ThemeService>();
            DarkModeToggle.IsChecked = themeService.CurrentMode == "dark";
            LightModeToggle.IsChecked = themeService.CurrentMode == "light";
        }
        catch (Exception)
        {
            // Service may not be available yet
        }
    }

    private void DarkModeToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var themeService = App.Services.GetRequiredService<ThemeService>();
            themeService.Apply(themeService.CurrentThemeId, "dark");
            ModeStatusText.Text = "Switched to dark mode.";
            DarkModeToggle.IsChecked = true;
            LightModeToggle.IsChecked = false;
        }
        catch (Exception ex)
        {
            ModeStatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void LightModeToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var themeService = App.Services.GetRequiredService<ThemeService>();
            themeService.Apply(themeService.CurrentThemeId, "light");
            ModeStatusText.Text = "Switched to light mode.";
            LightModeToggle.IsChecked = true;
            DarkModeToggle.IsChecked = false;
        }
        catch (Exception ex)
        {
            ModeStatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void ResetThemeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var themeService = App.Services.GetRequiredService<ThemeService>();
            var catalog = App.Services.GetRequiredService<IThemeCatalog>();
            var defaultTheme = catalog.BuiltIn.FirstOrDefault(t => t.Id == "avex.dark")
                               ?? catalog.BuiltIn.FirstOrDefault();

            if (defaultTheme is null)
            {
                ResetStatusText.Text = "No built-in themes available to reset to.";
                return;
            }

            themeService.Apply(defaultTheme.Id, defaultTheme.Mode);
            ResetStatusText.Text = $"Reset to {defaultTheme.Name} ({defaultTheme.Mode}).";
            RefreshState();
        }
        catch (Exception ex)
        {
            ResetStatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void RefreshWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var wallpaper = App.Services.GetRequiredService<IWallpaperService>();
            var state = wallpaper.Read();
            if (string.IsNullOrWhiteSpace(state.WallpaperPath))
            {
                WallpaperStatusText.Text = "No wallpaper is currently set.";
                return;
            }

            // Re-apply the current wallpaper to refresh it
            wallpaper.Apply(state);
            WallpaperStatusText.Text = $"Wallpaper refreshed: {state.WallpaperPath}";
        }
        catch (Exception ex)
        {
            WallpaperStatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var cacheDir = AvexPaths.CacheDir;
            if (Directory.Exists(cacheDir))
            {
                var files = Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories);
                int count = files.Length;
                foreach (var file in files)
                {
                    try { File.Delete(file); } catch { }
                }
                MaintenanceStatusText.Text = $"Cleared {count} cached file(s).";
            }
            else
            {
                MaintenanceStatusText.Text = "Cache directory does not exist; nothing to clear.";
            }
        }
        catch (Exception ex)
        {
            MaintenanceStatusText.Text = $"Error clearing cache: {ex.Message}";
        }
    }

    private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logDir = AvexPaths.LogDir;
            if (Directory.Exists(logDir))
            {
                var files = Directory.GetFiles(logDir, "*.log", SearchOption.AllDirectories);
                int count = files.Length;
                foreach (var file in files)
                {
                    try { File.Delete(file); } catch { }
                }
                MaintenanceStatusText.Text = $"Cleared {count} log file(s).";
            }
            else
            {
                MaintenanceStatusText.Text = "Log directory does not exist; nothing to clear.";
            }
        }
        catch (Exception ex)
        {
            MaintenanceStatusText.Text = $"Error clearing logs: {ex.Message}";
        }
    }
}
