using System.Reflection;
using System.Text.Json;
using AVEXOS.Core;

namespace AVEXOS.ThemeEngine;

public sealed class BuiltInThemeCatalog : IThemeCatalog
{
    private readonly IThemeValidator _validator;
    private readonly Lazy<IReadOnlyList<ThemeDocument>> _themes;

    public BuiltInThemeCatalog(IThemeValidator validator)
    {
        _validator = validator;
        _themes = new Lazy<IReadOnlyList<ThemeDocument>>(LoadAll);
    }

    public IReadOnlyList<ThemeDocument> BuiltIn => _themes.Value;

    private IReadOnlyList<ThemeDocument> LoadAll()
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
}

public interface IThemeCatalog
{
    IReadOnlyList<ThemeDocument> BuiltIn { get; }
}
