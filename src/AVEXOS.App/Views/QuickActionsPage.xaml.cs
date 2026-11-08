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

        try
        {
            var presService = App.Services.GetRequiredService<IPresentationModeService>();
            var config = presService.GetConfig();
            PresentationModeToggle.IsChecked = presService.IsEnabled;
            PresentationModeToggle.Content = presService.IsEnabled ? "Disable" : "Enable";
            BlankColorInput.Text = config.BlankColor;
            IncludePrimaryCheck.IsChecked = config.BlankPrimaryToo;
            PresentationStatusBadge.Text = presService.IsEnabled ? "[ACTIVE]" : "";
        }
        catch (Exception)
        {
            // Service may not be available yet
        }

        try
        {
            var focusService = App.Services.GetRequiredService<IFocusModeService>();
            var focusConfig = focusService.GetConfig();
            FocusModeToggle.IsChecked = focusService.IsEnabled;
            FocusModeToggle.Content = focusService.IsEnabled ? "Disable" : "Enable";
            DimLevelSlider.Value = focusConfig.DimLevel;
            DimLevelValue.Text = $"{focusConfig.DimLevel}%";
            UseSolidColorCheck.IsChecked = focusConfig.UseSolidColor;
            SolidColorInput.Text = focusConfig.SolidColor;
            BlurWallpaperCheck.IsChecked = focusConfig.BlurWallpaper;
            FocusModeStatusBadge.Text = focusService.IsEnabled ? "[ACTIVE]" : "";
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

    private void PresentationModeToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var presService = App.Services.GetRequiredService<IPresentationModeService>();
            var color = BlankColorInput.Text?.Trim() ?? "#000000";
            var includePrimary = IncludePrimaryCheck.IsChecked == true;

            var config = new PresentationModeConfig(presService.IsEnabled, color, includePrimary);
            presService.SaveConfig(config);

            if (presService.IsEnabled)
            {
                presService.Disable();
                PresentationModeToggle.IsChecked = false;
                PresentationModeToggle.Content = "Enable";
                PresentationStatusBadge.Text = "";
                PresentationStatusText.Text = "Presentation mode disabled. Wallpapers restored.";
            }
            else
            {
                presService.Enable();
                PresentationModeToggle.IsChecked = true;
                PresentationModeToggle.Content = "Disable";
                PresentationStatusBadge.Text = "[ACTIVE]";
                PresentationStatusText.Text = "Presentation mode active. Secondary monitors blanked.";
            }

            // Update tray icon
            UpdateTrayPresentationState(presService.IsEnabled);
        }
        catch (Exception ex)
        {
            PresentationStatusText.Text = $"Error: {ex.Message}";
        }
    }

    private static void UpdateTrayPresentationState(bool isActive)
    {
        try
        {
            // TrayIconService is managed by App; we rely on App-level wiring for tooltip updates
            // The keyboard shortcut handler also calls this via the App-level callback
        }
        catch
        {
            // Non-critical
        }
    }

    private void FocusModeToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var focusService = App.Services.GetRequiredService<IFocusModeService>();

            // Save current UI settings to config
            var config = focusService.GetConfig();
            config.DimLevel = (int)DimLevelSlider.Value;
            config.UseSolidColor = UseSolidColorCheck.IsChecked == true;
            config.SolidColor = SolidColorInput.Text?.Trim() ?? "#000000";
            config.BlurWallpaper = BlurWallpaperCheck.IsChecked == true;
            focusService.SaveConfig(config);

            if (focusService.IsEnabled)
            {
                focusService.Disable();
                FocusModeToggle.IsChecked = false;
                FocusModeToggle.Content = "Enable";
                FocusModeStatusBadge.Text = "";
                FocusModeStatusText.Text = "Focus mode disabled. Wallpapers restored.";
            }
            else
            {
                focusService.Enable();
                FocusModeToggle.IsChecked = true;
                FocusModeToggle.Content = "Disable";
                FocusModeStatusBadge.Text = "[ACTIVE]";
                FocusModeStatusText.Text = "Focus mode active. Non-primary monitors dimmed.";
            }
        }
        catch (Exception ex)
        {
            FocusModeStatusText.Text = $"Error: {ex.Message}";
        }
    }
}
