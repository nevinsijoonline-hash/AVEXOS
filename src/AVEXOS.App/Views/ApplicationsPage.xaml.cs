using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AVEXOS.Core;
using AVEXOS.WindowsIntegration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AVEXOS.App;

public sealed partial class ApplicationsPage : Page
{
    public ApplicationsPage()
    {
        InitializeComponent();
        Loaded += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var discovery = App.Services.GetRequiredService<IAppDiscoveryService>();
            var apps = await discovery.DiscoverAsync();
            AppList.ItemsSource = apps;
            StatusText.Text = $"{apps.Count} applications discovered.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }
}
