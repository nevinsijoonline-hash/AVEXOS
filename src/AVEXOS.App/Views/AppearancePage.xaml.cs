using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AVEXOS.Core;
using AVEXOS.WindowsIntegration;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using Windows.UI;

namespace AVEXOS.App;

public sealed partial class AppearancePage : Page
{
    private const string WallpaperTarget = "windows.desktop.wallpaper";
    private static readonly Regex HexColorRegex = new(@"^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$", RegexOptions.Compiled);
    private readonly List<MonitorInfo> _monitors = new();
    private MonitorInfo? _selectedMonitor;
    private readonly ObservableCollection<string> _slideshowPaths = new();

    public AppearancePage()
    {
        InitializeComponent();
        Loaded += async (s, e) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var wallpaper = App.Services.GetRequiredService<IWallpaperService>();

            // Populate monitor list
            _monitors.Clear();
            _monitors.AddRange(wallpaper.GetMonitors());

            MonitorSelector.Items.Clear();
            foreach (var monitor in _monitors)
            {
                var label = monitor.IsPrimary
                    ? $"{monitor.DisplayName} ({monitor.Width}x{monitor.Height}) [Primary]"
                    : $"{monitor.DisplayName} ({monitor.Width}x{monitor.Height})";
                MonitorSelector.Items.Add(label);
            }

            // Select first monitor by default
            if (_monitors.Count > 0)
            {
                MonitorSelector.SelectedIndex = 0;
            }
            else
            {
                // No monitors detected - show global wallpaper
                var state = wallpaper.Read();
                CurrentWallpaper.Text = string.IsNullOrWhiteSpace(state.WallpaperPath)
                    ? "(No wallpaper set)"
                    : state.WallpaperPath;
                MonitorInfoPanel.Visibility = Visibility.Collapsed;
                UpdatePreview(state.WallpaperPath);
            }
        }
        catch (Exception ex)
        {
            CurrentWallpaper.Text = $"Error: {ex.Message}";
        }
        await Task.CompletedTask;
    }

    private void MonitorSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = MonitorSelector.SelectedIndex;
        if (index < 0 || index >= _monitors.Count) return;

        _selectedMonitor = _monitors[index];
        UpdateMonitorInfo();
        _ = RefreshMonitorWallpaperAsync();
    }

    private void UpdateMonitorInfo()
    {
        if (_selectedMonitor is null)
        {
            MonitorInfoPanel.Visibility = Visibility.Collapsed;
            return;
        }

        MonitorInfoPanel.Visibility = Visibility.Visible;
        var m = _selectedMonitor;
        MonitorDetails.Text = $"{m.DisplayName}  |  {m.Width}x{m.Height}  |  Position: ({m.Left}, {m.Top})  |  {(m.IsPrimary ? "Primary" : "Secondary")}";
    }

    private async Task RefreshMonitorWallpaperAsync()
    {
        if (_selectedMonitor is null) return;

        try
        {
            var wallpaper = App.Services.GetRequiredService<IWallpaperService>();
            var monitorState = wallpaper.ReadMonitor(_selectedMonitor.DeviceId);

            if (monitorState is not null)
            {
                CurrentWallpaper.Text = string.IsNullOrWhiteSpace(monitorState.WallpaperPath)
                    ? "(No wallpaper set)"
                    : monitorState.WallpaperPath;
                UpdatePreview(monitorState.WallpaperPath);
            }
            else
            {
                CurrentWallpaper.Text = "(Monitor not found)";
                WallpaperPreview.Source = null;
            }

            // Load slideshow state for this monitor
            await LoadSlideshowStateAsync();
        }
        catch (Exception ex)
        {
            CurrentWallpaper.Text = $"Error: {ex.Message}";
        }
    }

    private void UpdatePreview(string? wallpaperPath)
    {
        if (!string.IsNullOrEmpty(wallpaperPath) && File.Exists(wallpaperPath))
        {
            try
            {
                var bitmap = new BitmapImage(new Uri(wallpaperPath));
                WallpaperPreview.Source = bitmap;
            }
            catch
            {
                WallpaperPreview.Source = null;
            }
        }
        else
        {
            WallpaperPreview.Source = null;
        }
    }

    private async Task<bool> ConfirmAsync(string title, string body, string primary)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = body,
            PrimaryButtonText = primary,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var wallpaper = App.Services.GetRequiredService<IWallpaperService>();
            var pipeline = App.Services.GetRequiredService<ChangePipeline>();
            var assetPath = Path.Combine(AppContext.BaseDirectory, "assets", "wallpapers", "avex-logo-wallpaper.png");

            if (!File.Exists(assetPath))
            {
                StatusText.Text = "Wallpaper asset not found.";
                return;
            }

            var importedPath = wallpaper.ImportWallpaper(assetPath);
            var newState = new AppearanceState(importedPath, 10, false);

            if (_selectedMonitor is not null)
            {
                // Apply to selected monitor only
                var originalState = wallpaper.ReadMonitor(_selectedMonitor.DeviceId);
                var targetLabel = $"{WallpaperTarget}.monitor.{_selectedMonitor.DeviceId}";
                var request = new ChangeRequest(targetLabel, "Wallpaper",
                    $"Apply AVEX wallpaper to {_selectedMonitor.DisplayName}", RiskLevel.Medium, importedPath);

                var result = await pipeline.ExecuteAsync(
                    request,
                    capture: () => (object?)originalState,
                    apply: _ => wallpaper.ApplyToMonitor(_selectedMonitor.DeviceId, newState),
                    verify: () => MatchesMonitor(wallpaper, _selectedMonitor.DeviceId, importedPath),
                    restore: _ =>
                    {
                        if (originalState is not null)
                            wallpaper.ApplyToMonitor(_selectedMonitor.DeviceId,
                                new AppearanceState(originalState.WallpaperPath, originalState.WallpaperStyle, originalState.TileWallpaper));
                    },
                    approve: req => ConfirmAsync(
                        "Apply Wallpaper",
                        $"This will change the wallpaper on {_selectedMonitor.DisplayName}. The original will be backed up and can be restored.",
                        "Apply"),
                    verifyRollback: () => originalState is not null && MatchesMonitor(wallpaper, _selectedMonitor.DeviceId, originalState.WallpaperPath));

                StatusText.Text = result.Detail;
            }
            else
            {
                // Apply globally (no monitor selected / single monitor fallback)
                var originalState = wallpaper.Read();
                var request = new ChangeRequest(WallpaperTarget, "Wallpaper", "Apply AVEX wallpaper", RiskLevel.Medium, importedPath);

                var result = await pipeline.ExecuteAsync(
                    request,
                    capture: () => originalState,
                    apply: _ => wallpaper.Apply(newState),
                    verify: () => Matches(wallpaper, importedPath),
                    restore: _ => wallpaper.Apply(originalState),
                    approve: req => ConfirmAsync(
                        "Apply Wallpaper",
                        "This will change your desktop wallpaper. The original will be backed up and can be restored.",
                        "Apply"),
                    verifyRollback: () => Matches(wallpaper, originalState.WallpaperPath));

                StatusText.Text = result.Detail;
            }

            await RefreshMonitorWallpaperAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private async void ApplyToAllButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var wallpaper = App.Services.GetRequiredService<IWallpaperService>();
            var pipeline = App.Services.GetRequiredService<ChangePipeline>();
            var assetPath = Path.Combine(AppContext.BaseDirectory, "assets", "wallpapers", "avex-logo-wallpaper.png");

            if (!File.Exists(assetPath))
            {
                StatusText.Text = "Wallpaper asset not found.";
                return;
            }

            var importedPath = wallpaper.ImportWallpaper(assetPath);
            var newState = new AppearanceState(importedPath, 10, false);
            var originalState = wallpaper.Read();

            var request = new ChangeRequest(WallpaperTarget, "Wallpaper",
                "Apply AVEX wallpaper to all monitors", RiskLevel.Medium, importedPath);

            var result = await pipeline.ExecuteAsync(
                request,
                capture: () => originalState,
                apply: _ => wallpaper.ApplyToAllMonitors(newState),
                verify: () => Matches(wallpaper, importedPath),
                restore: _ => wallpaper.Apply(originalState),
                approve: req => ConfirmAsync(
                    "Apply Wallpaper to All Monitors",
                    "This will set the same wallpaper on all connected monitors. The original will be backed up and can be restored.",
                    "Apply to All"),
                verifyRollback: () => Matches(wallpaper, originalState.WallpaperPath));

            StatusText.Text = result.Detail;
            await RefreshMonitorWallpaperAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var wallpaper = App.Services.GetRequiredService<IWallpaperService>();
            var backups = App.Services.GetRequiredService<IBackupStore>();
            var pipeline = App.Services.GetRequiredService<ChangePipeline>();

            var snapshot = backups.List(50).FirstOrDefault(b => b.TargetId == WallpaperTarget);
            if (snapshot is null)
            {
                StatusText.Text = "No backup available to restore from.";
                return;
            }

            var saved = backups.Load<AppearanceState>(snapshot.Id);
            if (saved is null)
            {
                StatusText.Text = $"Backup {snapshot.Id} could not be read.";
                return;
            }

            if (_selectedMonitor is not null)
            {
                // Restore for selected monitor
                var originalState = wallpaper.ReadMonitor(_selectedMonitor.DeviceId);
                var targetLabel = $"{WallpaperTarget}.monitor.{_selectedMonitor.DeviceId}";
                var request = new ChangeRequest(targetLabel, "Wallpaper",
                    $"Restore wallpaper on {_selectedMonitor.DisplayName} from {snapshot.Id}", RiskLevel.Low);

                var result = await pipeline.ExecuteAsync(
                    request,
                    capture: () => (object?)originalState,
                    apply: _ => wallpaper.ApplyToMonitor(_selectedMonitor.DeviceId, saved),
                    verify: () => MatchesMonitor(wallpaper, _selectedMonitor.DeviceId, saved.WallpaperPath),
                    restore: _ =>
                    {
                        if (originalState is not null)
                            wallpaper.ApplyToMonitor(_selectedMonitor.DeviceId,
                                new AppearanceState(originalState.WallpaperPath, originalState.WallpaperStyle, originalState.TileWallpaper));
                    },
                    approve: req => Task.FromResult(true),
                    verifyRollback: () => originalState is not null && MatchesMonitor(wallpaper, _selectedMonitor.DeviceId, originalState.WallpaperPath));

                StatusText.Text = result.Detail;
            }
            else
            {
                // Restore globally
                var originalState = wallpaper.Read();
                var request = new ChangeRequest(WallpaperTarget, "Wallpaper",
                    $"Restore wallpaper from {snapshot.Id}", RiskLevel.Low);

                var result = await pipeline.ExecuteAsync(
                    request,
                    capture: () => originalState,
                    apply: _ => wallpaper.Apply(saved),
                    verify: () => Matches(wallpaper, saved.WallpaperPath),
                    restore: _ => wallpaper.Apply(originalState),
                    approve: req => Task.FromResult(true),
                    verifyRollback: () => Matches(wallpaper, originalState.WallpaperPath));

                StatusText.Text = result.Detail;
            }

            await RefreshMonitorWallpaperAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private static bool Matches(IWallpaperService wallpaper, string expectedPath)
    {
        var current = wallpaper.Read();
        return string.Equals(current.WallpaperPath, expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesMonitor(IWallpaperService wallpaper, string deviceId, string expectedPath)
    {
        var current = wallpaper.ReadMonitor(deviceId);
        return current is not null && string.Equals(current.WallpaperPath, expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Accent Color Picker ----

    private void AccentHexInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyHexPreview();
    }

    private void ApplyHexPreview()
    {
        var hex = AccentHexInput.Text.Trim();
        if (HexColorRegex.IsMatch(hex))
        {
            var color = ParseColor(hex);
            AccentPreviewBorder.Background = new SolidColorBrush(color);
            AccentPreviewButton.Background = new SolidColorBrush(color);
            AccentPreviewProgress.Foreground = new SolidColorBrush(color);
            AccentPreviewTitle.Foreground = new SolidColorBrush(color);
        }
    }

    private void PresetAccent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            AccentHexInput.Text = hex;
            ApplyHexPreview();
        }
    }

    private void SaveCustomAccent_Click(object sender, RoutedEventArgs e)
    {
        var hex = AccentHexInput.Text.Trim();
        if (!HexColorRegex.IsMatch(hex))
        {
            AccentStatusText.Text = "Enter a valid hex color (e.g. #6E8BFF).";
            return;
        }

        try
        {
            var settings = App.Services.GetRequiredService<ISettingsStore>();
            settings.Set("user.accent.color", hex);
            AccentStatusText.Text = $"Custom accent color {hex} saved.";
        }
        catch (Exception ex)
        {
            AccentStatusText.Text = $"Error saving: {ex.Message}";
        }
    }

    private static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            var r = Convert.ToByte(hex[0..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);
            return Color.FromArgb(255, r, g, b);
        }
        else if (hex.Length == 8)
        {
            var a = Convert.ToByte(hex[0..2], 16);
            var r = Convert.ToByte(hex[2..4], 16);
            var g = Convert.ToByte(hex[4..6], 16);
            var b = Convert.ToByte(hex[6..8], 16);
            return Color.FromArgb(a, r, g, b);
        }
        return Colors.Transparent;
    }

    // ---- Wallpaper Slideshow ----

    private async Task LoadSlideshowStateAsync()
    {
        if (_selectedMonitor is null) return;
        try
        {
            var slideshow = App.Services.GetRequiredService<IMonitorSlideshowService>();
            SlideshowToggle.IsOn = slideshow.IsEnabled(_selectedMonitor.DeviceId);

            var interval = slideshow.GetIntervalMinutes(_selectedMonitor.DeviceId);
            SelectIntervalCombo(interval);

            _slideshowPaths.Clear();
            foreach (var p in slideshow.GetPaths(_selectedMonitor.DeviceId))
                _slideshowPaths.Add(p);
            SlideshowWallpaperList.ItemsSource = _slideshowPaths;
        }
        catch (Exception ex)
        {
            SlideshowStatusText.Text = $"Error loading slideshow: {ex.Message}";
        }
        await Task.CompletedTask;
    }

    private void SelectIntervalCombo(int minutes)
    {
        for (int i = 0; i < SlideshowIntervalCombo.Items.Count; i++)
        {
            if (SlideshowIntervalCombo.Items[i] is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out var v) && v == minutes)
            {
                SlideshowIntervalCombo.SelectedIndex = i;
                return;
            }
        }
        SlideshowIntervalCombo.SelectedIndex = 1; // Default 30 min
    }

    private void SlideshowToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_selectedMonitor is null) return;
        try
        {
            var slideshow = App.Services.GetRequiredService<IMonitorSlideshowService>();
            if (SlideshowToggle.IsOn)
                slideshow.Enable(_selectedMonitor.DeviceId);
            else
                slideshow.Disable(_selectedMonitor.DeviceId);

            SlideshowStatusText.Text = SlideshowToggle.IsOn
                ? "Slideshow enabled."
                : "Slideshow disabled.";
        }
        catch (Exception ex)
        {
            SlideshowStatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void SlideshowInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectedMonitor is null) return;
        if (SlideshowIntervalCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out var minutes))
        {
            try
            {
                var slideshow = App.Services.GetRequiredService<IMonitorSlideshowService>();
                slideshow.SetIntervalMinutes(_selectedMonitor.DeviceId, minutes);
                SlideshowStatusText.Text = $"Interval set to {minutes} minutes.";
            }
            catch (Exception ex)
            {
                SlideshowStatusText.Text = $"Error: {ex.Message}";
            }
        }
    }

    private async void AddSlideshowWallpaper_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMonitor is null) return;
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail
            };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Main);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            var wallpaper = App.Services.GetRequiredService<IWallpaperService>();
            var imported = wallpaper.ImportWallpaper(file.Path);

            var slideshow = App.Services.GetRequiredService<IMonitorSlideshowService>();
            var current = slideshow.GetPaths(_selectedMonitor.DeviceId).ToList();
            if (!current.Contains(imported, StringComparer.OrdinalIgnoreCase))
            {
                current.Add(imported);
                slideshow.SetPaths(_selectedMonitor.DeviceId, current);
            }

            _slideshowPaths.Add(imported);
            SlideshowStatusText.Text = $"Added: {imported}";
        }
        catch (Exception ex)
        {
            SlideshowStatusText.Text = $"Error adding wallpaper: {ex.Message}";
        }
    }

    private void RemoveSlideshowWallpaper_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMonitor is null) return;
        var selected = SlideshowWallpaperList.SelectedItem as string;
        if (selected is null)
        {
            SlideshowStatusText.Text = "Select a wallpaper to remove.";
            return;
        }

        try
        {
            var slideshow = App.Services.GetRequiredService<IMonitorSlideshowService>();
            var current = slideshow.GetPaths(_selectedMonitor.DeviceId).ToList();
            current.RemoveAll(p => string.Equals(p, selected, StringComparison.OrdinalIgnoreCase));
            slideshow.SetPaths(_selectedMonitor.DeviceId, current);

            _slideshowPaths.Remove(selected);
            SlideshowStatusText.Text = $"Removed: {selected}";
        }
        catch (Exception ex)
        {
            SlideshowStatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void AdvanceSlideshow_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMonitor is null) return;
        try
        {
            var slideshow = App.Services.GetRequiredService<IMonitorSlideshowService>();
            var result = slideshow.Advance(_selectedMonitor.DeviceId);
            SlideshowStatusText.Text = result is not null
                ? $"Advanced to: {result}"
                : "No wallpapers in slideshow or slideshow is disabled.";
        }
        catch (Exception ex)
        {
            SlideshowStatusText.Text = $"Error: {ex.Message}";
        }
    }
}
