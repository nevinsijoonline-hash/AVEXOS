using System.Text.RegularExpressions;
using AVEXOS.Core;

namespace AVEXOS.ThemeEngine;

public sealed class ThemeValidator : IThemeValidator
{
    private static readonly Regex IdRegex = new(@"^[a-z][a-z0-9]*(\.[a-z0-9]+)+$", RegexOptions.Compiled);
    private static readonly Regex VersionRegex = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);
    private static readonly Regex ColorRegex = new(@"^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$", RegexOptions.Compiled);
    private static readonly HashSet<string> RequiredColors = new(StringComparer.Ordinal)
    {
        "bg.base", "bg.raised", "surface.1", "text.primary", "text.secondary", "accent", "border.subtle"
    };
    private static readonly HashSet<string> AllowedLevels = new(StringComparer.Ordinal) { "A", "B", "C", "D" };

    public ValidationResult Validate(ThemeDocument theme)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(theme.Id) || !IdRegex.IsMatch(theme.Id))
            errors.Add("Id is required and must match 'a.b.c' with lowercase alphanumerics.");
        if (string.IsNullOrWhiteSpace(theme.Name))
            errors.Add("Name is required.");
        if (string.IsNullOrWhiteSpace(theme.Version) || !VersionRegex.IsMatch(theme.Version))
            errors.Add("Version must be semver (e.g. 1.0.0).");
        if (theme.Mode is not ("dark" or "light"))
            errors.Add("Mode must be 'dark' or 'light'.");
        if (string.IsNullOrWhiteSpace(theme.FontFamily))
            errors.Add("FontFamily is required.");

        foreach (var key in RequiredColors)
        {
            if (!theme.Color.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Required color token missing: {key}");
            }
            else if (!ColorRegex.IsMatch(value))
            {
                errors.Add($"Color '{key}' must be a hex value (#RRGGBB or #RRGGBBAA).");
            }
        }

        foreach (var (key, value) in theme.Color)
        {
            if (!ColorRegex.IsMatch(value))
            {
                errors.Add($"Color '{key}' is not a valid hex value.");
            }
        }

        foreach (var (key, value) in theme.Radius)
        {
            if (value < 0 || value > 64) errors.Add($"Radius '{key}' must be 0..64.");
        }
        foreach (var (key, value) in theme.Space)
        {
            if (value < 0 || value > 256) errors.Add($"Space '{key}' must be 0..256.");
        }
        foreach (var (key, value) in theme.Motion)
        {
            if (value < 0 || value > 2000) errors.Add($"Motion '{key}' must be 0..2000 ms.");
        }

        foreach (var (key, target) in theme.Targets)
        {
            if (!AllowedLevels.Contains(target.Level))
            {
                errors.Add($"Target '{key}' has unknown level '{target.Level}'.");
            }
            else if (string.Equals(target.Level, "D", StringComparison.Ordinal))
            {
                errors.Add($"Target '{key}' is Level D (unsupported); themes must not reference unsupported targets.");
            }
        }

        if (errors.Count == 0 && theme.Targets.Count == 0)
        {
            warnings.Add("Theme declares no targets; it will not apply to any surface.");
        }

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }
}

public interface IThemeValidator
{
    ValidationResult Validate(ThemeDocument theme);
}
