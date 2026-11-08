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
    private DispatcherTimer? _sessionTimer;

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
            var sysInfo = App.Services.GetRequiredService<SystemInfoService>();

            // Status cards
            var theme = catalog.BuiltIn.FirstOrDefault(t => t.Id == themeService.CurrentThemeId);
            ThemeName.Text = theme?.Name ?? "Unknown";
            ThemeMode.Text = themeService.CurrentMode;
            WindowsBuild.Text = sysInfo.WindowsBuild;

            var apps = await discovery.DiscoverAsync();
            AppCount.Text = apps.Count.ToString();
            SupportedCount.Text = apps.Count(a => a.Capability != CapabilityLevel.D).ToString();
            BackupCount.Text = backups.List().Count.ToString();
            PendingCount.Text = operations.CountByState(ChangeState.Pending).ToString();

            // System Information Dashboard
            SysWindowsVersion.Text = sysInfo.WindowsVersion;
            SysDotNetVersion.Text = sysInfo.DotNetVersion;
            SysAppVersion.Text = sysInfo.AppVersion;
            SysThemeId.Text = sysInfo.CurrentThemeId;
            SysThemeMode.Text = sysInfo.CurrentThemeMode;
            SysWallpaperPath.Text = sysInfo.ActiveWallpaperPath;
            SysMachineName.Text = sysInfo.MachineName;
            SysUserName.Text = sysInfo.UserName;
            SysProcessorCount.Text = sysInfo.ProcessorCount.ToString();
            SysTotalMemory.Text = $"{sysInfo.TotalMemoryMb} MB";

            // Session duration with live update
            UpdateSessionDuration(sysInfo);
            _sessionTimer?.Stop();
            _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _sessionTimer.Tick += (sender, args) => UpdateSessionDuration(sysInfo);
            _sessionTimer.Start();

            ActivityList.ItemsSource = audit.Recent(20);
        }
        catch (Exception)
        {
            // Log but don't crash
        }
    }

    private void UpdateSessionDuration(SystemInfoService sysInfo)
    {
        SessionDuration.Text = sysInfo.SessionDurationDisplay;
    }
}
