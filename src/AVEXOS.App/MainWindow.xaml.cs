using System.Runtime.InteropServices;
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
                "QuickActions" => typeof(QuickActionsPage),
                "Settings" => typeof(SettingsPage),
                _ => typeof(HomePage)
            };
            ContentFrame.Navigate(page);
        }
    }

    /// <summary>Navigate the main frame to the Settings page (used by global shortcuts).</summary>
    public void NavigateToSettings()
    {
        foreach (var item in NavView.FooterMenuItems)
        {
            if (item is NavigationViewItem ni && ni.Tag?.ToString() == "Settings")
            {
                NavView.SelectedItem = ni;
                return;
            }
        }
        ContentFrame.Navigate(typeof(SettingsPage));
    }

    /// <summary>
    /// Restores the window (if minimized), brings it to the foreground, and activates it.
    /// Used by the system tray icon "Open" command.
    /// </summary>
    public void ShowAndActivate()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Restore if minimized
        ShowWindow(hwnd, SW_RESTORE);

        // Bring to foreground
        Activate();

        // Set foreground window (required for taskbar flash to stop)
        SetForegroundWindow(hwnd);
    }

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
