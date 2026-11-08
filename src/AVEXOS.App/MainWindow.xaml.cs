using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AVEXOS.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(HomePage));
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString() ?? "";
            Type page = tag switch
            {
                "Home" => typeof(HomePage),
                "Appearance" => typeof(AppearancePage),
                "Themes" => typeof(ThemesPage),
                "Applications" => typeof(ApplicationsPage),
                "Activity" => typeof(ActivityPage),
                _ => typeof(HomePage)
            };
            ContentFrame.Navigate(page);
        }
    }
}
