using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AVEXOS.Core;
using AVEXOS.WindowsIntegration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AVEXOS.App;

public sealed partial class AppearancePage : Page
{
    private const string WallpaperTarget = "windows.desktop.wallpaper";

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
            var state = wallpaper.Read();
            CurrentWallpaper.Text = string.IsNullOrWhiteSpace(state.WallpaperPath)
                ? "(No wallpaper set)"
                : state.WallpaperPath;
        }
        catch (Exception ex)
        {
            CurrentWallpaper.Text = $"Error: {ex.Message}";
        }
        await Task.CompletedTask;
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
            var assetPath = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "wallpapers", "avex-wallpaper.png");

            if (!System.IO.File.Exists(assetPath))
            {
                StatusText.Text = "Wallpaper asset not found.";
                return;
            }

            var importedPath = wallpaper.ImportWallpaper(assetPath);
            var newState = new AppearanceState(importedPath, 10, false);
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
            await RefreshAsync();
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

            // capture() runs before apply(), so the newest backup for this target
            // holds the wallpaper as it was before that change.
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

            var originalState = wallpaper.Read();

            // No file is written here, so PathToWrite stays null; the registry
            // write is guarded by RegistryPolicy inside WallpaperService.Apply.
            var request = new ChangeRequest(WallpaperTarget, "Wallpaper",
                $"Restore wallpaper from {snapshot.Id}", RiskLevel.Low);

            var result = await pipeline.ExecuteAsync(
                request,
                capture: () => originalState,
                apply: _ => wallpaper.Apply(saved),
                verify: () => Matches(wallpaper, saved.WallpaperPath),
                restore: _ => wallpaper.Apply(originalState),
                // Clicking Restore is itself the instruction to undo, so no
                // second confirmation is raised.
                approve: req => Task.FromResult(true),
                verifyRollback: () => Matches(wallpaper, originalState.WallpaperPath));

            StatusText.Text = result.Detail;
            await RefreshAsync();
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
}
