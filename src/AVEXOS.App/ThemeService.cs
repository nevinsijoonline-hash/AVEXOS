using AVEXOS.Core;
using AVEXOS.ThemeEngine;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AVEXOS.App;

public sealed class ThemeService
{
    private readonly ISettingsStore _settings;
    private readonly IThemeCatalog _catalog;
    private string _currentThemeId = "avex.dark";
    private string _currentMode = "dark";
    private ResourceDictionary? _appliedThemeDictionary;

    public ThemeService(ISettingsStore settings, IThemeCatalog catalog)
    {
        _settings = settings;
        _catalog = catalog;
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
        if (root is null) return;

        root.RequestedTheme = mode == "light" ? ElementTheme.Light : ElementTheme.Dark;

        var theme = _catalog.BuiltIn.FirstOrDefault(t => t.Id == themeId);
        if (theme is null) return;

        var resourceSet = ThemeResourceBuilder.Build(theme);
        var dictionary = BuildResourceDictionary(resourceSet, mode);

        if (_appliedThemeDictionary is not null)
        {
            root.Resources.MergedDictionaries.Remove(_appliedThemeDictionary);
        }

        root.Resources.MergedDictionaries.Add(dictionary);
        _appliedThemeDictionary = dictionary;
    }

    private static ResourceDictionary BuildResourceDictionary(ThemeResourceSet resources, string mode)
    {
        var dict = new ResourceDictionary();

        var themeDict = new ResourceDictionary { ["Key"] = mode == "light" ? "Light" : "Dark" };
        foreach (var (key, hex) in resources.ColorHex)
        {
            themeDict[key] = ParseColor(hex);
        }
        foreach (var (brushKey, colorKey) in resources.BrushSource)
        {
            var color = (Color)themeDict[colorKey];
            themeDict[brushKey] = new SolidColorBrush(color);
        }
        foreach (var (key, value) in resources.Scalars)
        {
            if (key.Contains("Radius"))
                themeDict[key] = new CornerRadius(value);
            else
                themeDict[key] = value;
        }

        var themeDictionaries = new ResourceDictionary();
        themeDictionaries.ThemeDictionaries.Add(themeDict["Key"], themeDict);
        dict.MergedDictionaries.Add(themeDictionaries);

        dict["AvexFontFamily"] = new FontFamily(resources.FontFamily);

        return dict;
    }

    private static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            var r = Convert.ToByte(hex[0..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);
            return Color.FromArgb(255, r, g, b);
        }
        else if (hex.Length == 8)
        {
            var a = Convert.ToByte(hex[0..2], 16);
            var r = Convert.ToByte(hex[2..4], 16);
            var g = Convert.ToByte(hex[4..6], 16);
            var b = Convert.ToByte(hex[6..8], 16);
            return Color.FromArgb(a, r, g, b);
        }
        return Colors.Transparent;
    }
}
