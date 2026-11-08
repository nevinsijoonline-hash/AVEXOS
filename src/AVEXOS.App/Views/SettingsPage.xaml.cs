using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using AVEXOS.Backup;
using AVEXOS.Core;

namespace AVEXOS.App;

public sealed partial class SettingsPage : Page
{
    private KeyboardShortcutService? _shortcutService;
    private SettingsBackupService? _backupService;
    private ThemeService? _themeService;
    private IAutoBrightnessService? _autoBrightnessService;
    private bool _suppressAutoSave;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += async (s, e) => await InitAsync();
    }

    private async Task InitAsync()
    {
        _shortcutService = App.Services.GetService<KeyboardShortcutService>();
        _backupService = App.Services.GetService<SettingsBackupService>();
        _themeService = App.Services.GetService<ThemeService>();
        _autoBrightnessService = App.Services.GetService<IAutoBrightnessService>();
        RefreshDisplay();
        RefreshBackupList();
        RefreshAutoBrightnessUI();
        await Task.CompletedTask;
    }

    // ---- refresh UI from service state -----------------------------------

    private void RefreshDisplay()
    {
        _shortcutService = App.Services.GetService<KeyboardShortcutService>();
        if (_shortcutService is null) return;

        ToggleThemeShortcut.Text   = _shortcutService.GetShortcutDisplay(HotkeyAction.ToggleTheme);
        NextWallpaperShortcut.Text = _shortcutService.GetShortcutDisplay(HotkeyAction.NextWallpaper);
        OpenPanelShortcut.Text     = _shortcutService.GetShortcutDisplay(HotkeyAction.OpenPanel);
        OpenSettingsShortcut.Text  = _shortcutService.GetShortcutDisplay(HotkeyAction.OpenSettings);
    }

    // ---- change shortcut button click ------------------------------------

    private async void ChangeShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string actionName) return;
        if (!Enum.TryParse<HotkeyAction>(actionName, true, out var action)) return;
        if (_shortcutService is null) return;

        var current = _shortcutService.GetShortcutDisplay(action);
        var dialog = BuildRecorderDialog(action, current);
        await dialog.ShowAsync();
    }

    // ---- shortcut recorder dialog ----------------------------------------

    private ContentDialog BuildRecorderDialog(HotkeyAction action, string currentShortcut)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 320 };

        var instruction = new TextBlock
        {
            Text = $"Press the new key combination for \"{GetActionLabel(action)}\".",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
        };

        var currentLabel = new TextBlock
        {
            Text = $"Current: {currentShortcut}",
            FontSize = 13,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AvexTextSecondaryBrush"],
        };

        var shortcutDisplay = new TextBlock
        {
            Text = "Press keys...",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 8),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AvexAccentBrush"],
        };

        var errorText = new TextBlock
        {
            Text = "",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AvexDangerBrush"],
            Visibility = Visibility.Collapsed,
        };

        // Track pressed modifier keys
        var heldModifiers = new HashSet<VirtualKey>();
        string? recordedShortcut = null;

        // Invisible element to capture key events
        var keyCatcher = new TextBox
        {
            IsReadOnly = true,
            Opacity = 0.01,
            Height = 1,
            Width = 1,
            Margin = new Thickness(0, -2, 0, 0),
            PreventKeyboardDisplayOnProgrammaticFocus = true,
        };

        keyCatcher.KeyDown += (_, ke) =>
        {
            var key = ke.Key;

            // Track modifiers
            if (IsModifierKey(key))
            {
                heldModifiers.Add(key);
                UpdateDisplay();
                ke.Handled = true;
                return;
            }

            // Non-modifier key pressed – build the shortcut string
            var combo = BuildShortcutString(heldModifiers, key);
            recordedShortcut = combo;

            // Validate
            if (!ShortcutParser.IsValid(combo))
            {
                errorText.Text = "Include at least one modifier (Ctrl, Shift, or Alt).";
                errorText.Visibility = Visibility.Visible;
                recordedShortcut = null;
            }
            else if (ShortcutParser.IsSystemReserved(combo))
            {
                errorText.Text = $"'{combo}' is reserved by Windows.";
                errorText.Visibility = Visibility.Visible;
                recordedShortcut = null;
            }
            else if (_shortcutService is not null &&
                     ShortcutParser.ConflictsWith(combo, _shortcutService.Shortcuts, action))
            {
                errorText.Text = $"'{combo}' is already assigned to another action.";
                errorText.Visibility = Visibility.Visible;
                recordedShortcut = null;
            }
            else
            {
                errorText.Visibility = Visibility.Collapsed;
            }

            UpdateDisplay();
            ke.Handled = true;
        };

        keyCatcher.KeyUp += (_, ke) =>
        {
            if (IsModifierKey(ke.Key))
                heldModifiers.Remove(ke.Key);
            UpdateDisplay();
            ke.Handled = true;
        };

        void UpdateDisplay()
        {
            if (heldModifiers.Count > 0 && recordedShortcut is null)
            {
                shortcutDisplay.Text = BuildPartialModifierString(heldModifiers) + "+...";
            }
            else if (recordedShortcut is not null)
            {
                shortcutDisplay.Text = recordedShortcut;
            }
            else
            {
                shortcutDisplay.Text = "Press keys...";
            }
        }

        panel.Children.Add(instruction);
        panel.Children.Add(currentLabel);
        panel.Children.Add(shortcutDisplay);
        panel.Children.Add(keyCatcher);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = "Record Keyboard Shortcut",
            Content = panel,
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        // Focus the key catcher when the dialog opens
        dialog.Opened += (_, _) =>
        {
            keyCatcher.Focus(FocusState.Programmatic);
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (recordedShortcut is null || _shortcutService is null)
            {
                args.Cancel = true;
                return;
            }

            var error = _shortcutService.SetShortcut(action, recordedShortcut);
            if (error is not null)
            {
                errorText.Text = error;
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            RefreshDisplay();
        };

        return dialog;
    }

    // ---- reset to defaults -----------------------------------------------

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _shortcutService?.ResetToDefaults();
        RefreshDisplay();
        StatusText.Text = "Shortcuts reset to defaults.";
    }

    // ---- helpers ---------------------------------------------------------

    private static string GetActionLabel(HotkeyAction action) => action switch
    {
        HotkeyAction.ToggleTheme   => "Toggle Dark / Light Mode",
        HotkeyAction.NextWallpaper => "Next Wallpaper",
        HotkeyAction.OpenPanel     => "Open AVEX Control Panel",
        HotkeyAction.OpenSettings  => "Open Settings",
        _ => action.ToString()
    };

    private static bool IsModifierKey(VirtualKey key)
        => key is VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu
           or VirtualKey.LeftControl or VirtualKey.RightControl
           or VirtualKey.LeftShift or VirtualKey.RightShift
           or VirtualKey.LeftMenu or VirtualKey.RightMenu
           or VirtualKey.LeftWindows or VirtualKey.RightWindows;

    private static string BuildShortcutString(HashSet<VirtualKey> modifiers, VirtualKey key)
    {
        var parts = new List<string>();
        // Normalise left/right variants
        bool ctrl  = modifiers.Any(k => k is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl);
        bool shift = modifiers.Any(k => k is VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift);
        bool alt   = modifiers.Any(k => k is VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu);
        bool win   = modifiers.Any(k => k is VirtualKey.LeftWindows or VirtualKey.RightWindows);

        if (ctrl)  parts.Add("Ctrl");
        if (shift) parts.Add("Shift");
        if (alt)   parts.Add("Alt");
        if (win)   parts.Add("Win");
        parts.Add(VirtualKeyToDisplayString(key));
        return string.Join("+", parts);
    }

    private static string BuildPartialModifierString(HashSet<VirtualKey> modifiers)
    {
        var parts = new List<string>();
        bool ctrl  = modifiers.Any(k => k is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl);
        bool shift = modifiers.Any(k => k is VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift);
        bool alt   = modifiers.Any(k => k is VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu);
        bool win   = modifiers.Any(k => k is VirtualKey.LeftWindows or VirtualKey.RightWindows);
        if (ctrl)  parts.Add("Ctrl");
        if (shift) parts.Add("Shift");
        if (alt)   parts.Add("Alt");
        if (win)   parts.Add("Win");
        return string.Join("+", parts);
    }

    private static string VirtualKeyToDisplayString(VirtualKey key)
    {
        // Letters
        var vk = (uint)key;
        if (vk >= 0x41 && vk <= 0x5A)
            return ((char)vk).ToString();

        // Digits 0-9
        if (vk >= 0x30 && vk <= 0x39)
            return ((char)vk).ToString();

        // Function keys F1-F24
        if (vk >= 0x70 && vk <= 0x87)
            return $"F{vk - 0x70 + 1}";

        return key switch
        {
            VirtualKey.Space       => "Space",
            VirtualKey.Enter       => "Enter",
            VirtualKey.Tab         => "Tab",
            VirtualKey.Escape      => "Escape",
            VirtualKey.Delete      => "Delete",
            VirtualKey.Insert      => "Insert",
            VirtualKey.Home        => "Home",
            VirtualKey.End         => "End",
            VirtualKey.PageUp      => "PageUp",
            VirtualKey.PageDown    => "PageDown",
            VirtualKey.Up          => "Up",
            VirtualKey.Down        => "Down",
            VirtualKey.Left        => "Left",
            VirtualKey.Right       => "Right",
            VirtualKey.NumberPad0  => "NumPad0",
            VirtualKey.NumberPad1  => "NumPad1",
            VirtualKey.NumberPad2  => "NumPad2",
            VirtualKey.NumberPad3  => "NumPad3",
            VirtualKey.NumberPad4  => "NumPad4",
            VirtualKey.NumberPad5  => "NumPad5",
            VirtualKey.NumberPad6  => "NumPad6",
            VirtualKey.NumberPad7  => "NumPad7",
            VirtualKey.NumberPad8  => "NumPad8",
            VirtualKey.NumberPad9  => "NumPad9",
            _ => key.ToString()
        };
    }

    // ---- Settings Backup & Restore ----------------------------------------

    private void RefreshBackupList()
    {
        if (_backupService is null) return;

        var backups = _backupService.ListBackups();
        var displayItems = backups.Select(b => new BackupDisplayItem(b)).ToList();

        BackupList.ItemsSource = displayItems;
        EmptyStateText.Visibility = displayItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BackupList.Visibility = displayItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        HistorySubtitle.Text = displayItems.Count == 0
            ? "No backups yet"
            : $"{displayItems.Count} backup(s) — max 10 retained";
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_backupService is null) return;

        try
        {
            BackupButton.IsEnabled = false;
            var info = _backupService.CreateBackup();
            BackupStatusText.Text = $"Backup created: {info.FileName} ({FormatFileSize(info.FileSizeBytes)}) — {info.SettingCount} settings saved.";
            RefreshBackupList();
        }
        catch (Exception ex)
        {
            BackupStatusText.Text = $"Backup failed: {ex.Message}";
        }
        finally
        {
            BackupButton.IsEnabled = true;
        }
    }

    private async void RestoreFromFileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".json");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Main);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            await RestoreFromFileAsync(file.Path);
        }
        catch (Exception ex)
        {
            BackupStatusText.Text = $"Error opening file picker: {ex.Message}";
        }
    }

    private async Task RestoreFromFileAsync(string filePath)
    {
        if (_backupService is null) return;

        try
        {
            var settings = _backupService.ReadBackup(filePath);

            var preview = string.Join("\n", settings.Take(20).Select(kv => $"  {kv.Key} = {TruncateValue(kv.Value)}"));
            if (settings.Count > 20)
                preview += $"\n  ... and {settings.Count - 20} more settings";

            var dialog = new ContentDialog
            {
                Title = "Restore Settings Preview",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = $"This will restore {settings.Count} setting(s):\n\n{preview}\n\nCurrent settings will be overwritten. Continue?",
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                    },
                    MaxHeight = 400,
                    Padding = new Thickness(0, 8, 0, 0)
                },
                PrimaryButtonText = "Restore",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var restoredKeys = _backupService.RestoreBackup(filePath);
            BackupStatusText.Text = $"Restored {restoredKeys.Count} settings from {System.IO.Path.GetFileName(filePath)}.";

            // Re-apply theme from restored settings to reflect changes immediately
            var settingsStore = App.Services.GetService<ISettingsStore>();
            if (_themeService is not null && settingsStore is not null)
            {
                var themeId = settingsStore.Get("theme.id") ?? _themeService.CurrentThemeId;
                var mode = settingsStore.Get("theme.mode") ?? _themeService.CurrentMode;
                _themeService.Apply(themeId, mode);
            }

            RefreshBackupList();
        }
        catch (Exception ex)
        {
            BackupStatusText.Text = $"Restore failed: {ex.Message}";
        }
    }

    private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string filePath) return;
        await RestoreFromFileAsync(filePath);
    }

    private async void PreviewBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_backupService is null) return;
        if (sender is not Button btn || btn.Tag is not string filePath) return;

        try
        {
            var settings = _backupService.ReadBackup(filePath);
            var preview = string.Join("\n", settings.Select(kv => $"  {kv.Key} = {TruncateValue(kv.Value)}"));

            var dialog = new ContentDialog
            {
                Title = $"Backup Preview — {System.IO.Path.GetFileName(filePath)}",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = $"{settings.Count} setting(s):\n\n{preview}",
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                    },
                    MaxHeight = 500,
                    Padding = new Thickness(0, 8, 0, 0)
                },
                CloseButtonText = "Close",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            BackupStatusText.Text = $"Preview failed: {ex.Message}";
        }
    }

    private async void DeleteBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_backupService is null) return;
        if (sender is not Button btn || btn.Tag is not string filePath) return;

        var fileName = System.IO.Path.GetFileName(filePath);
        var dialog = new ContentDialog
        {
            Title = "Delete Backup",
            Content = $"Are you sure you want to delete '{fileName}'? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        try
        {
            if (_backupService.DeleteBackup(filePath))
            {
                BackupStatusText.Text = $"Deleted backup: {fileName}";
                RefreshBackupList();
            }
            else
            {
                BackupStatusText.Text = $"Backup file not found: {fileName}";
            }
        }
        catch (Exception ex)
        {
            BackupStatusText.Text = $"Delete failed: {ex.Message}";
        }
    }

    private static string TruncateValue(string value, int maxLen = 60)
    {
        if (value.Length <= maxLen) return value;
        return value[..maxLen] + "...";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    // ---- Auto-Brightness UI ------------------------------------------------

    private void RefreshAutoBrightnessUI()
    {
        if (_autoBrightnessService is null) return;

        _suppressAutoSave = true;
        var config = _autoBrightnessService.GetConfig();

        AutoBrightnessToggle.IsOn = config.IsEnabled;
        DayBrightnessSlider.Value = config.DayBrightness;
        NightBrightnessSlider.Value = config.NightBrightness;
        DayBrightnessValue.Text = $"{config.DayBrightness}%";
        NightBrightnessValue.Text = $"{config.NightBrightness}%";
        TransitionSlider.Value = config.TransitionMinutes;
        TransitionValue.Text = $"{config.TransitionMinutes} min";

        DayStartTimePicker.SelectedTime = config.DayStartTime;
        NightStartTimePicker.SelectedTime = config.NightStartTime;

        var current = _autoBrightnessService.GetCurrentBrightness();
        CurrentBrightnessRun.Text = current >= 0 ? $"{current}%" : "--";

        _suppressAutoSave = false;
    }

    private void AutoBrightnessToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_autoBrightnessService is null || _suppressAutoSave) return;

        if (AutoBrightnessToggle.IsOn)
            _autoBrightnessService.Enable();
        else
            _autoBrightnessService.Disable();

        AutoBrightnessStatus.Text = AutoBrightnessToggle.IsOn
            ? "Auto-brightness enabled."
            : "Auto-brightness disabled.";
    }

    private void SaveCurrentConfig()
    {
        if (_autoBrightnessService is null || _suppressAutoSave) return;

        var config = _autoBrightnessService.GetConfig();
        config.DayBrightness = (int)DayBrightnessSlider.Value;
        config.NightBrightness = (int)NightBrightnessSlider.Value;
        config.TransitionMinutes = (int)TransitionSlider.Value;
        if (DayStartTimePicker.SelectedTime is TimeSpan dayTime)
            config.DayStartTime = dayTime;
        if (NightStartTimePicker.SelectedTime is TimeSpan nightTime)
            config.NightStartTime = nightTime;
        config.IsEnabled = AutoBrightnessToggle.IsOn;

        _autoBrightnessService.SaveConfig(config);
    }

    private void DayBrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_suppressAutoSave) return;
        DayBrightnessValue.Text = $"{(int)e.NewValue}%";
        SaveCurrentConfig();
    }

    private void NightBrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_suppressAutoSave) return;
        NightBrightnessValue.Text = $"{(int)e.NewValue}%";
        SaveCurrentConfig();
    }

    private void TransitionSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_suppressAutoSave) return;
        TransitionValue.Text = $"{(int)e.NewValue} min";
        SaveCurrentConfig();
    }

    private void DayStartTimePicker_SelectedTimeChanged(object sender, TimePickerSelectedValueChangedEventArgs e)
    {
        SaveCurrentConfig();
    }

    private void NightStartTimePicker_SelectedTimeChanged(object sender, TimePickerSelectedValueChangedEventArgs e)
    {
        SaveCurrentConfig();
    }

    private void PreviewBrightness_Click(object sender, RoutedEventArgs e)
    {
        if (_autoBrightnessService is null) return;

        // Calculate what brightness would be right now using the service
        var now = DateTimeOffset.Now;
        var target = _autoBrightnessService.CalculateBrightness(now);
        _autoBrightnessService.ApplyBrightness(target);
        CurrentBrightnessRun.Text = $"{target}%";
        AutoBrightnessStatus.Text = $"Preview: applied {target}% brightness.";
    }

    private void ApplyNow_Click(object sender, RoutedEventArgs e)
    {
        if (_autoBrightnessService is null) return;

        SaveCurrentConfig();
        var now = DateTimeOffset.Now;
        var target = _autoBrightnessService.CalculateBrightness(now);
        _autoBrightnessService.ApplyBrightness(target);
        CurrentBrightnessRun.Text = $"{target}%";
        AutoBrightnessStatus.Text = $"Applied {target}% brightness.";
    }
}

/// <summary>Display model for backup list items.</summary>
public sealed class BackupDisplayItem
{
    public string FileName { get; }
    public string FilePath { get; }
    public string CreatedUtc { get; }
    public int SettingCount { get; }
    public string FileSizeDisplay { get; }

    public BackupDisplayItem(SettingsBackupInfo info)
    {
        FileName = info.FileName;
        FilePath = info.FilePath;
        CreatedUtc = info.CreatedUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        SettingCount = info.SettingCount;
        FileSizeDisplay = FormatSize(info.FileSizeBytes);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
