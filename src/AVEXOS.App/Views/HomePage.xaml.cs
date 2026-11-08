using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AVEXOS.Core;
using AVEXOS.ThemeEngine;
using AVEXOS.WindowsIntegration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AVEXOS.App;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        Loaded += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var themeService = App.Services.GetRequiredService<ThemeService>();
            var catalog = App.Services.GetRequiredService<IThemeCatalog>();
            var discovery = App.Services.GetRequiredService<IAppDiscoveryService>();
            var backups = App.Services.GetRequiredService<IBackupStore>();
            var operations = App.Services.GetRequiredService<IOperationStore>();
            var audit = App.Services.GetRequiredService<IAuditLog>();

            var theme = catalog.BuiltIn.FirstOrDefault(t => t.Id == themeService.CurrentThemeId);
            ThemeName.Text = theme?.Name ?? "Unknown";
            ThemeMode.Text = themeService.CurrentMode;
            WindowsBuild.Text = $"Build {Environment.OSVersion.Version.Build}";

            var apps = await discovery.DiscoverAsync();
            AppCount.Text = apps.Count.ToString();
            SupportedCount.Text = apps.Count(a => a.Capability != CapabilityLevel.D).ToString();
            BackupCount.Text = backups.List().Count.ToString();
            PendingCount.Text = operations.CountByState(ChangeState.Pending).ToString();

            ActivityList.ItemsSource = audit.Recent(20);
        }
        catch (Exception)
        {
            // Log but don't crash
        }
    }
}
