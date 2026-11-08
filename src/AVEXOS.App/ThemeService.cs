using AVEXOS.Core;
using AVEXOS.ThemeEngine;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AVEXOS.App;

public sealed class ThemeService
{
    private readonly ISettingsStore _settings;
    private string _currentThemeId = "avex.dark";
    private string _currentMode = "dark";

    public ThemeService(ISettingsStore settings)
    {
        _settings = settings;
        var saved = settings.Get("theme.id");
        if (!string.IsNullOrEmpty(saved)) _currentThemeId = saved;
        var mode = settings.Get("theme.mode");
        if (!string.IsNullOrEmpty(mode)) _currentMode = mode;
    }

    public string CurrentThemeId => _currentThemeId;
    public string CurrentMode => _currentMode;

    public void Apply(string themeId, string mode)
    {
        _currentThemeId = themeId;
        _currentMode = mode;
        _settings.Set("theme.id", themeId);
        _settings.Set("theme.mode", mode);
        var root = App.Main?.Content as FrameworkElement;
        if (root is not null)
        {
            root.RequestedTheme = mode == "light" ? ElementTheme.Light : ElementTheme.Dark;
        }
    }
}
