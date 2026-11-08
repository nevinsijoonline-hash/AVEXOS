using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using AVEXOS.Core;
using AVEXOS.ThemeEngine;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.UI;

namespace AVEXOS.App;

public sealed partial class ThemesPage : Page
{
    private ThemeDisplayItem? _selectedItem;
    private ThemeSchedulerService? _scheduler;
    private readonly ObservableCollection<ScheduleDisplayItem> _scheduleDisplay = new();
    private ResourceDictionary? _previewDictionary;

    public ThemesPage()
    {
        InitializeComponent();
        Loaded += (s, e) => LoadThemes();
    }

    private void LoadThemes()
    {
        var catalog = App.Services.GetRequiredService<IThemeCatalog>();
        var userIds = new HashSet<string>(catalog.UserThemes.Select(t => t.Id), StringComparer.OrdinalIgnoreCase);

        var displayItems = catalog.All.Select(t => new ThemeDisplayItem
        {
            Theme = t,
            Name = t.Name,
            Description = t.Description,
            Source = userIds.Contains(t.Id) ? "[user]" : "[built-in]"
        }).ToList();

        ThemeList.ItemsSource = displayItems;
        if (displayItems.Count > 0) ThemeList.SelectedIndex = 0;

        _scheduler = App.Services.GetRequiredService<ThemeSchedulerService>();
        SchedulerToggle.IsOn = _scheduler.IsEnabled;

        // Populate theme combo in scheduler
        ScheduleThemeCombo.ItemsSource = catalog.All
            .Select(t => new ComboBoxItem { Content = t.Name, Tag = t.Id }).ToList();
        if (catalog.All.Count > 0) ScheduleThemeCombo.SelectedIndex = 0;

        RefreshScheduleList();
    }

    private void RefreshScheduleList()
    {
        if (_scheduler is null) return;
        var catalog = App.Services.GetRequiredService<IThemeCatalog>();
        _scheduleDisplay.Clear();
        foreach (var s in _scheduler.Schedules)
        {
            var themeName = catalog.All.FirstOrDefault(t => t.Id == s.ThemeId)?.Name ?? s.ThemeId;
            _scheduleDisplay.Add(new ScheduleDisplayItem
            {
                Id = s.Id,
                TimeDisplay = $"{s.Time.Hours:D2}:{s.Time.Minutes:D2}",
                ThemeName = themeName,
                Mode = s.Mode
            });
        }
        ScheduleList.ItemsSource = _scheduleDisplay;
    }

    private void ThemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedItem = ThemeList.SelectedItem as ThemeDisplayItem;
    }

    private void PreviewThemeButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = _selectedItem?.Theme;
        if (theme is null) return;

        // Build preview resource dictionary from the selected theme
        var resourceSet = ThemeResourceBuilder.Build(theme);
        var previewDict = BuildPreviewDictionary(resourceSet, theme.Mode);

        // Remove old preview dictionary if present
        if (_previewDictionary is not null)
        {
            PreviewSurface.Resources.MergedDictionaries.Remove(_previewDictionary);
        }

        // Apply preview to the preview surface only (not the whole app)
        PreviewSurface.Resources.MergedDictionaries.Add(previewDict);
        _previewDictionary = previewDict;

        // Update preview theme name
        PreviewThemeName.Text = $"{theme.Name} ({theme.Mode})";

        // Build color swatches
        BuildColorSwatches(theme);

        // Apply preview colors to sample elements
        ApplyPreviewColors(resourceSet, theme.Mode);

        // Show the preview area
        PreviewBorder.Visibility = Visibility.Visible;
    }

    private void ClosePreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_previewDictionary is not null)
        {
            PreviewSurface.Resources.MergedDictionaries.Remove(_previewDictionary);
            _previewDictionary = null;
        }
        PreviewBorder.Visibility = Visibility.Collapsed;
    }

    private void ApplyThemeButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = _selectedItem?.Theme;
        if (theme is null) return;
        var themeService = App.Services.GetRequiredService<ThemeService>();
        themeService.Apply(theme.Id, theme.Mode);
        StatusText.Text = $"Applied: {theme.Name} ({theme.Mode})";

        // Close preview after applying
        ClosePreviewButton_Click(sender, e);
    }

    private void BuildColorSwatches(ThemeDocument theme)
    {
        ColorSwatchPanel.Children.Clear();
        foreach (var (key, hex) in theme.Color)
        {
            var swatch = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 8, 0) };
            var colorBox = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(ParseColor(hex)),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1)
            };
            var label = new TextBlock
            {
                Text = key,
                FontSize = 9,
                Foreground = (SolidColorBrush)Application.Current.Resources["AvexTextTertiaryBrush"],
                MaxWidth = 44,
                TextWrapping = TextWrapping.Wrap
            };
            swatch.Children.Add(colorBox);
            swatch.Children.Add(label);
            ColorSwatchPanel.Children.Add(swatch);
        }
    }

    private void ApplyPreviewColors(ThemeResourceSet resources, string mode)
    {
        // Apply background and text colors to the preview surface
        if (resources.ColorHex.TryGetValue("AvexBgBaseColor", out var bgColorHex))
        {
            PreviewSurface.Background = new SolidColorBrush(ParseColor(bgColorHex));
        }
        if (resources.ColorHex.TryGetValue("AvexTextPrimaryColor", out var textColorHex))
        {
            var brush = new SolidColorBrush(ParseColor(textColorHex));
            PreviewTitle.Foreground = brush;
            PreviewBody.Foreground = brush;
        }
        if (resources.ColorHex.TryGetValue("AvexAccentColor", out var accentHex))
        {
            PreviewProgress.Foreground = new SolidColorBrush(ParseColor(accentHex));
        }
    }

    private static ResourceDictionary BuildPreviewDictionary(ThemeResourceSet resources, string mode)
    {
        var dict = new ResourceDictionary();
        foreach (var (key, hex) in resources.ColorHex)
        {
            dict[key] = ParseColor(hex);
        }
        foreach (var (brushKey, colorKey) in resources.BrushSource)
        {
            if (dict.TryGetValue(colorKey, out var colorObj) && colorObj is Color color)
            {
                dict[brushKey] = new SolidColorBrush(color);
            }
        }
        foreach (var (key, value) in resources.Scalars)
        {
            if (key.Contains("Radius"))
                dict[key] = new CornerRadius((int)value);
            else
                dict[key] = value;
        }
        return dict;
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

    private async void ImportThemeButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Main);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            ImportExportStatus.Text = "Import cancelled.";
            return;
        }

        try
        {
            var userThemeService = App.Services.GetRequiredService<UserThemeService>();
            var result = userThemeService.Import(file.Path);

            if (result.IsSuccess)
            {
                var warnings = result.Warnings.Count > 0
                    ? $" Warnings: {string.Join("; ", result.Warnings)}"
                    : "";
                ImportExportStatus.Text = $"Imported: {result.Theme!.Name}{warnings}";
                RefreshCatalogAndList();
            }
            else
            {
                ImportExportStatus.Text = $"Import failed: {string.Join("; ", result.Errors)}";
            }
        }
        catch (Exception ex)
        {
            ImportExportStatus.Text = $"Import error: {ex.Message}";
        }
    }

    private async void ExportThemeButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = _selectedItem?.Theme;
        if (theme is null)
        {
            ImportExportStatus.Text = "Select a theme to export first.";
            return;
        }

        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("JSON Theme", new List<string> { ".json" });
        picker.SuggestedFileName = $"{theme.Id}.json";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Main);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            ImportExportStatus.Text = "Export cancelled.";
            return;
        }

        try
        {
            var userThemeService = App.Services.GetRequiredService<UserThemeService>();
            userThemeService.Export(theme, file.Path);
            ImportExportStatus.Text = $"Exported: {theme.Name} -> {file.Name}";
        }
        catch (Exception ex)
        {
            ImportExportStatus.Text = $"Export error: {ex.Message}";
        }
    }

    private void DeleteThemeButton_Click(object sender, RoutedEventArgs e)
    {
        var item = _selectedItem;
        if (item?.Theme is null)
        {
            ImportExportStatus.Text = "Select a user theme to delete.";
            return;
        }

        if (item.Source != "[user]")
        {
            ImportExportStatus.Text = "Cannot delete built-in themes.";
            return;
        }

        var theme = item.Theme;
        try
        {
            var userThemeService = App.Services.GetRequiredService<UserThemeService>();
            if (userThemeService.Delete(theme.Id))
            {
                ImportExportStatus.Text = $"Deleted user theme: {theme.Name}";
                RefreshCatalogAndList();
            }
            else
            {
                ImportExportStatus.Text = $"Theme file not found for: {theme.Id}";
            }
        }
        catch (Exception ex)
        {
            ImportExportStatus.Text = $"Delete error: {ex.Message}";
        }
    }

    private void RefreshCatalogAndList()
    {
        var catalog = (BuiltInThemeCatalog)App.Services.GetRequiredService<IThemeCatalog>();
        catalog.RefreshUserThemes();
        LoadThemes();
    }

    private void SchedulerToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _scheduler?.SetEnabled(SchedulerToggle.IsOn);
    }

    private void AddScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_scheduler is null) return;
        var time = ScheduleTimePicker.Time;
        var themeItem = ScheduleThemeCombo.SelectedItem as ComboBoxItem;
        if (themeItem?.Tag is not string themeId) return;
        var mode = (ScheduleModeCombo.SelectedIndex == 1) ? "light" : "dark";
        _scheduler.AddSchedule(time, themeId, mode);
        RefreshScheduleList();
    }

    private void DeleteScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_scheduler is null) return;
        if ((sender as Button)?.Tag is string id)
        {
            _scheduler.RemoveSchedule(id);
            RefreshScheduleList();
        }
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        var prompt = PromptBox.Text;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            ProposalStatus.Text = "Enter a prompt first.";
            return;
        }

        try
        {
            var generator = App.Services.GetRequiredService<LocalProposalService>();
            var result = generator.Propose(prompt);
            if (result.IsValid)
            {
                ProposalStatus.Text = $"Generated: {result.Theme.Name} ({result.Theme.Mode})";
            }
            else
            {
                ProposalStatus.Text = $"Invalid: {string.Join("; ", result.Validation.Errors)}";
            }
        }
        catch (Exception ex)
        {
            ProposalStatus.Text = $"Error: {ex.Message}";
        }
    }
}

public sealed class ScheduleDisplayItem
{
    public string Id { get; set; } = "";
    public string TimeDisplay { get; set; } = "";
    public string ThemeName { get; set; } = "";
    public string Mode { get; set; } = "";
}

/// <summary>
/// Display wrapper for theme list items, adding a source label.
/// </summary>
public sealed class ThemeDisplayItem
{
    public ThemeDocument Theme { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>"[built-in]" or "[user]"</summary>
    public string Source { get; set; } = "";
}
