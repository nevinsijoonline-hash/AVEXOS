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
    private const string ConsentKey = "app.discovery.consent";

    public ApplicationsPage()
    {
        InitializeComponent();
        Loaded += async (s, e) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var settings = App.Services.GetRequiredService<ISettingsStore>();
            var consent = settings.Get(ConsentKey);
            
            if (consent != "true")
            {
                var consentGiven = await ShowConsentDialogAsync();
                if (!consentGiven)
                {
                    StatusText.Text = "App discovery requires your consent to scan installed software.";
                    return;
                }
                settings.Set(ConsentKey, "true");
            }

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

    private async Task<bool> ShowConsentDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "App Discovery Consent",
            Content = "This feature scans your installed software inventory from the Windows registry to display a list of applications. This data stays local and is not transmitted. Do you consent to running app discovery?",
            PrimaryButtonText = "I Consent",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }
}
