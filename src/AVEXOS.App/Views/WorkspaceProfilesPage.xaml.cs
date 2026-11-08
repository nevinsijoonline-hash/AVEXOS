using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using AVEXOS.Core;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AVEXOS.App;

public sealed partial class WorkspaceProfilesPage : Page
{
    public WorkspaceProfilesPage()
    {
        InitializeComponent();
        Loaded += (s, e) => RefreshProfiles();
    }

    private void RefreshProfiles()
    {
        ProfileCardsPanel.Children.Clear();

        try
        {
            var service = App.Services.GetRequiredService<IWorkspaceProfileService>();
            var profiles = service.GetAll().OrderBy(p => p.Name).ToList();

            if (profiles.Count == 0)
            {
                ListStatus.Text = "No profiles saved yet. Create one above to get started.";
                return;
            }

            ListStatus.Text = "";

            foreach (var profile in profiles)
            {
                ProfileCardsPanel.Children.Add(BuildProfileCard(profile, service));
            }
        }
        catch (Exception ex)
        {
            ListStatus.Text = $"Error loading profiles: {ex.Message}";
        }
    }

    private static FrameworkElement BuildProfileCard(WorkspaceProfile profile, IWorkspaceProfileService service)
    {
        var border = new Border
        {
            Background = (Brush)Application.Current.Resources["AvexSurface1Brush"],
            BorderBrush = (Brush)Application.Current.Resources["AvexBorderSubtleBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        // Left side: profile info
        var infoPanel = new StackPanel { Spacing = 4 };

        var nameHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var nameText = new TextBlock
        {
            Text = profile.Name,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold
        };
        nameHeader.Children.Add(nameText);

        if (profile.IsDefault)
        {
            var badge = new TextBlock
            {
                Text = "DEFAULT",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["AvexTextTertiaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            nameHeader.Children.Add(badge);
        }

        var detailsText = new TextBlock
        {
            Text = $"Theme: {profile.ThemeId}  |  Mode: {profile.ThemeMode}  |  Wallpaper: {Path.GetFileName(profile.WallpaperPath)}",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["AvexTextSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap
        };

        var createdText = new TextBlock
        {
            Text = $"Created: {profile.CreatedUtc:yyyy-MM-dd HH:mm} UTC",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["AvexTextTertiaryBrush"]
        };

        infoPanel.Children.Add(nameHeader);
        infoPanel.Children.Add(detailsText);
        infoPanel.Children.Add(createdText);

        // Right side: action buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        var applyBtn = new Button { Content = "Apply" };
        applyBtn.Style = (Style)Application.Current.Resources["AccentButtonStyle"];

        var defaultBtn = new Button { Content = "Set Default" };
        if (profile.IsDefault) defaultBtn.IsEnabled = false;

        var deleteBtn = new Button { Content = "Delete" };

        buttonPanel.Children.Add(applyBtn);
        buttonPanel.Children.Add(defaultBtn);
        buttonPanel.Children.Add(deleteBtn);

        Grid.SetColumn(infoPanel, 0);
        Grid.SetColumn(buttonPanel, 1);
        root.Children.Add(infoPanel);
        root.Children.Add(buttonPanel);
        border.Child = root;

        // Wire up click handlers
        applyBtn.Click += (_, _) =>
        {
            try
            {
                service.Apply(profile.Id);
            }
            catch { }
        };

        defaultBtn.Click += (_, _) =>
        {
            try
            {
                service.SetDefault(profile.Id);
            }
            catch { }
        };

        deleteBtn.Click += (_, _) =>
        {
            try
            {
                service.Delete(profile.Id);
            }
            catch { }
        };

        return border;
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NewProfileName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            CreateStatus.Text = "Please enter a profile name.";
            return;
        }

        try
        {
            var service = App.Services.GetRequiredService<IWorkspaceProfileService>();
            var wallpaper = App.Services.GetRequiredService<IWallpaperService>();
            var themeService = App.Services.GetRequiredService<ThemeService>();

            var currentWallpaper = wallpaper.Read();
            var profile = new WorkspaceProfile(
                Id: Guid.NewGuid().ToString("N"),
                Name: name,
                WallpaperPath: currentWallpaper.WallpaperPath,
                ThemeId: themeService.CurrentThemeId,
                ThemeMode: themeService.CurrentMode,
                CreatedUtc: DateTimeOffset.UtcNow,
                IsDefault: false);

            service.Save(profile);
            CreateStatus.Text = $"Profile '{name}' created successfully.";
            NewProfileName.Text = "";
            RefreshProfiles();
        }
        catch (Exception ex)
        {
            CreateStatus.Text = $"Error creating profile: {ex.Message}";
        }
    }
}
