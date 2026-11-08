using System.Text;

namespace AVEXOS.ThemeEngine;

public sealed record ThemeResourceSet(
    IReadOnlyDictionary<string, string> ColorHex,
    IReadOnlyDictionary<string, string> BrushSource,
    IReadOnlyDictionary<string, double> Scalars,
    string FontFamily);

public static class ThemeResourceBuilder
{
    public static ThemeResourceSet Build(ThemeDocument theme)
    {
        var colorHex = new Dictionary<string, string>(StringComparer.Ordinal);
        var brushSource = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, hex) in theme.Color)
        {
            var pascal = ToPascalCase(key);
            var colorKey = $"Avex{pascal}Color";
            var brushKey = $"Avex{pascal}Brush";
            colorHex[colorKey] = hex;
            brushSource[brushKey] = colorKey;
        }

        var scalars = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var (key, value) in theme.Radius)
            scalars[$"AvexRadius{ToPascalCase(key)}"] = value;

        foreach (var (key, value) in theme.Space)
            scalars[$"AvexSpace{ToPascalCase(key)}"] = value;

        foreach (var (key, value) in theme.Motion)
            scalars[$"AvexMotion{ToPascalCase(key)}"] = value;

        foreach (var (key, value) in theme.Type)
            scalars[$"AvexType{ToPascalCase(key)}"] = value;

        return new ThemeResourceSet(colorHex, brushSource, scalars, theme.FontFamily);
    }

    internal static string ToPascalCase(string dottedKey)
    {
        var sb = new StringBuilder();
        foreach (var segment in dottedKey.Split('.', '-'))
        {
            if (segment.Length == 0) continue;
            if (char.IsAsciiLetter(segment[0]))
                sb.Append(char.ToUpperInvariant(segment[0]));
            else
                sb.Append(segment[0]);
            for (int i = 1; i < segment.Length; i++)
                sb.Append(segment[i]);
        }
        return sb.ToString();
    }
}
