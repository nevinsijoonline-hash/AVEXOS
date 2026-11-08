using System.Reflection;
using System.Text.Json;
using AVEXOS.Core;

namespace AVEXOS.ThemeEngine;

public sealed class BuiltInThemeCatalog : IThemeCatalog
{
    private readonly IThemeValidator _validator;
    private readonly Lazy<IReadOnlyList<ThemeDocument>> _builtIn;
    private Lazy<IReadOnlyList<ThemeDocument>> _userThemes;
    private Lazy<IReadOnlyList<ThemeDocument>> _all;

    public BuiltInThemeCatalog(IThemeValidator validator)
    {
        _validator = validator;
        _builtIn = new Lazy<IReadOnlyList<ThemeDocument>>(LoadBuiltIn);
        _userThemes = new Lazy<IReadOnlyList<ThemeDocument>>(LoadUser);
        _all = new Lazy<IReadOnlyList<ThemeDocument>>(MergeAll);
    }

    public IReadOnlyList<ThemeDocument> BuiltIn => _builtIn.Value;
    public IReadOnlyList<ThemeDocument> UserThemes => _userThemes.Value;
    public IReadOnlyList<ThemeDocument> All => _all.Value;

    /// <summary>
    /// Force reload of user themes (call after import/delete).
    /// </summary>
    public void RefreshUserThemes()
    {
        // Reset lazy by creating new instances via reflection-free approach:
        // We re-create the lazy fields by reloading.
        _userThemes = new Lazy<IReadOnlyList<ThemeDocument>>(LoadUser);
        _all = new Lazy<IReadOnlyList<ThemeDocument>>(MergeAll);
    }

    private IReadOnlyList<ThemeDocument> LoadBuiltIn()
    {
        var assembly = typeof(BuiltInThemeCatalog).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("AVEXOS.ThemeEngine.Themes.", StringComparison.Ordinal)
                        && n.EndsWith(".json", StringComparison.Ordinal))
            .ToList();

        var themes = new List<ThemeDocument>();
        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;
            var doc = JsonSerializer.Deserialize<ThemeDocument>(stream, AvexJson.Options);
            if (doc is null) continue;
            var validation = _validator.Validate(doc);
            if (validation.IsValid) themes.Add(doc);
        }

        return themes.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private IReadOnlyList<ThemeDocument> LoadUser()
    {
        var userDir = UserThemeService.UserThemesDir;
        if (!Directory.Exists(userDir))
            return Array.Empty<ThemeDocument>();

        var files = Directory.GetFiles(userDir, "*.json");
        var themes = new List<ThemeDocument>();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var theme = JsonSerializer.Deserialize<ThemeDocument>(json, AvexJson.Options);
                if (theme is null) continue;
                var validation = _validator.Validate(theme);
                if (validation.IsValid) themes.Add(theme);
            }
            catch
            {
                // Skip invalid files
            }
        }

        return themes.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private IReadOnlyList<ThemeDocument> MergeAll()
    {
        // User themes override built-in themes with the same ID.
        var merged = new Dictionary<string, ThemeDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var theme in BuiltIn)
            merged[theme.Id] = theme;
        foreach (var theme in UserThemes)
            merged[theme.Id] = theme;
        return merged.Values.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public interface IThemeCatalog
{
    IReadOnlyList<ThemeDocument> BuiltIn { get; }
    IReadOnlyList<ThemeDocument> UserThemes { get; }
    /// <summary>
    /// Combined list: user themes override built-in themes with the same ID.
    /// </summary>
    IReadOnlyList<ThemeDocument> All { get; }
}
