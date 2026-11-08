namespace AVEXOS.ThemeEngine;

/// <summary>
/// Deterministic local proposal generator. Produces a validated ThemeDocument
/// from a natural-language request without calling an external AI provider.
/// The IAiProvider interface is reserved for a future provider-backed engine.
/// </summary>
public sealed class LocalProposalService
{
    private readonly IThemeValidator _validator;
    private readonly IThemeCatalog _catalog;

    public LocalProposalService(IThemeValidator validator, IThemeCatalog catalog)
    {
        _validator = validator;
        _catalog = catalog;
    }

    public ThemeProposalResult Propose(string prompt)
    {
        var lower = prompt.ToLowerInvariant();
        var mode = lower.Contains("light") ? "light" : "dark";
        var baseTheme = _catalog.BuiltIn.FirstOrDefault(t => string.Equals(t.Mode, mode, StringComparison.Ordinal))
                        ?? _catalog.BuiltIn.First();

        var accent = PickAccent(lower);
        var bg = PickBackground(lower, mode);
        var surface = PickSurface(lower, mode);
        var text = PickText(lower, mode);
        var border = PickBorder(lower, mode);
        var spacing = PickSpacing(lower);
        var corner = PickCorner(lower);

        var theme = new ThemeDocument
        {
            Id = $"avex.generated.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            Name = $"AVEX {SuggestName(lower)}",
            Version = "1.0.0",
            Mode = mode,
            Category = "generated",
            Author = "AVEX AI Design Studio (local)",
            Description = prompt,
            FontFamily = baseTheme.FontFamily,
            Color = new Dictionary<string, string>
            {
                ["bg.base"] = bg,
                ["bg.raised"] = Shift(bg, mode == "dark" ? 4 : -4),
                ["surface.1"] = surface,
                ["surface.2"] = Shift(surface, mode == "dark" ? 6 : -6),
                ["border.subtle"] = border,
                ["border.strong"] = Shift(border, mode == "dark" ? 10 : -10),
                ["text.primary"] = text,
                ["text.secondary"] = Shift(text, mode == "dark" ? -40 : 40),
                ["text.tertiary"] = Shift(text, mode == "dark" ? -80 : 80),
                ["accent"] = accent,
                ["accent.hover"] = Shift(accent, 10),
                ["success"] = "#3FA96B",
                ["warning"] = "#D9A23A",
                ["danger"] = "#D9534F"
            },
            Radius = new Dictionary<string, int> { ["sm"] = corner, ["md"] = corner + 1, ["lg"] = corner + 2 },
            Space = new Dictionary<string, int> { ["xs"] = spacing, ["sm"] = spacing * 2, ["md"] = spacing * 3, ["lg"] = spacing * 4, ["xl"] = spacing * 6 },
            Motion = baseTheme.Motion,
            Type = baseTheme.Type,
            Targets = new Dictionary<string, ThemeTarget>
            {
                ["avex.controlcenter"] = new() { Level = "A" },
                ["windows.desktop.wallpaper"] = new() { Level = "B" }
            }
        };

        var validation = _validator.Validate(theme);
        return new ThemeProposalResult(theme, validation);
    }

    private static string PickAccent(string lower) =>
        lower.Contains("graphite") ? "#6E8BFF" :
        lower.Contains("green") || lower.Contains("programming") ? "#3FA96B" :
        lower.Contains("warm") || lower.Contains("amber") ? "#D9A23A" :
        lower.Contains("minimal") ? "#9AA0A6" :
        lower.Contains("contrast") ? "#FF5B5B" :
        "#6E8BFF";

    private static string PickBackground(string lower, string mode) =>
        mode == "dark"
            ? (lower.Contains("graphite") ? "#111214" : "#141517")
            : (lower.Contains("warm") ? "#F6F3EE" : "#F4F5F7");

    private static string PickSurface(string lower, string mode) =>
        mode == "dark" ? "#1E2125" : "#FFFFFF";

    private static string PickText(string lower, string mode) =>
        mode == "dark" ? "#ECEDEF" : "#1C1F23";

    private static string PickBorder(string lower, string mode) =>
        mode == "dark" ? "#2C3137" : "#E1E4E8";

    private static int PickSpacing(string lower) =>
        lower.Contains("compact") ? 3 : lower.Contains("spacious") ? 6 : 4;

    private static int PickCorner(string lower) =>
        lower.Contains("round") || lower.Contains("soft") ? 4 :
        lower.Contains("sharp") ? 0 : 2;

    private static string SuggestName(string lower) =>
        lower.Contains("graphite") ? "Graphite" :
        lower.Contains("minimal") ? "Minimal" :
        lower.Contains("programming") ? "Programming" :
        lower.Contains("warm") ? "Warm" :
        lower.Contains("contrast") ? "Contrast" :
        "Custom";

    private static string Shift(string hex, int amount)
    {
        var rgb = ParseRgb(hex);
        if (rgb is null) return hex;
        var (r, g, b) = rgb.Value;
        r = Clamp(r + amount); g = Clamp(g + amount); b = Clamp(b + amount);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static (byte r, byte g, byte b)? ParseRgb(string hex)
    {
        if (hex.Length < 7 || hex[0] != '#') return null;
        if (!byte.TryParse(hex[1..3], System.Globalization.NumberStyles.HexNumber, null, out var r)) return null;
        if (!byte.TryParse(hex[3..5], System.Globalization.NumberStyles.HexNumber, null, out var g)) return null;
        if (!byte.TryParse(hex[5..7], System.Globalization.NumberStyles.HexNumber, null, out var b)) return null;
        return (r, g, b);
    }

    private static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);
}

public sealed record ThemeProposalResult(ThemeDocument Theme, ValidationResult Validation)
{
    public bool IsValid => Validation.IsValid;
}
