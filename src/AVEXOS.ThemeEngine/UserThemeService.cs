using System.Text.Json;
using AVEXOS.Core;

namespace AVEXOS.ThemeEngine;

/// <summary>
/// Manages user-imported themes: import from JSON file, export to JSON file,
/// delete, and load from the user themes directory.
/// </summary>
public sealed class UserThemeService
{
    private readonly IThemeValidator _validator;

    public UserThemeService(IThemeValidator validator)
    {
        _validator = validator;
    }

    /// <summary>
    /// Directory where user-imported themes are stored.
    /// </summary>
    public static string UserThemesDir => AvexPaths.UserThemeDir;

    /// <summary>
    /// Export a theme to a JSON file at the given path.
    /// </summary>
    public void Export(ThemeDocument theme, string filePath)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var json = JsonSerializer.Serialize(theme, AvexJson.Options);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Import a theme from a JSON file. Validates the theme and saves it
    /// to the user themes directory. Returns the validation result.
    /// </summary>
    public ThemeImportResult Import(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            return ThemeImportResult.Failure("File not found.");

        ThemeDocument? theme;
        try
        {
            var json = File.ReadAllText(filePath);
            theme = JsonSerializer.Deserialize<ThemeDocument>(json, AvexJson.Options);
        }
        catch (Exception ex)
        {
            return ThemeImportResult.Failure($"Failed to read theme file: {ex.Message}");
        }

        if (theme is null)
            return ThemeImportResult.Failure("File does not contain a valid theme.");

        var validation = _validator.Validate(theme);
        if (!validation.IsValid)
            return ThemeImportResult.Failure(validation.Errors);

        Directory.CreateDirectory(UserThemesDir);
        var destPath = Path.Combine(UserThemesDir, $"{theme.Id}.json");
        var jsonOut = JsonSerializer.Serialize(theme, AvexJson.Options);
        File.WriteAllText(destPath, jsonOut);

        return ThemeImportResult.Success(theme, validation.Warnings);
    }

    /// <summary>
    /// Delete a user theme by its ID. Returns true if the file existed and was deleted.
    /// </summary>
    public bool Delete(string themeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeId);
        var path = Path.Combine(UserThemesDir, $"{themeId}.json");
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    /// <summary>
    /// Load all user themes from the user themes directory.
    /// Invalid themes are silently skipped.
    /// </summary>
    public IReadOnlyList<ThemeDocument> LoadAll()
    {
        if (!Directory.Exists(UserThemesDir))
            return Array.Empty<ThemeDocument>();

        var files = Directory.GetFiles(UserThemesDir, "*.json");
        var themes = new List<ThemeDocument>();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var theme = JsonSerializer.Deserialize<ThemeDocument>(json, AvexJson.Options);
                if (theme is null) continue;
                var validation = _validator.Validate(theme);
                if (validation.IsValid)
                    themes.Add(theme);
            }
            catch
            {
                // Skip files that cannot be parsed
            }
        }

        return themes.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed class ThemeImportResult
{
    private ThemeImportResult(bool success, ThemeDocument? theme, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        IsSuccess = success;
        Theme = theme;
        Errors = errors;
        Warnings = warnings;
    }

    public bool IsSuccess { get; }
    public ThemeDocument? Theme { get; }
    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }

    public static ThemeImportResult Success(ThemeDocument theme, IReadOnlyList<string>? warnings = null)
        => new(true, theme, Array.Empty<string>(), warnings ?? Array.Empty<string>());

    public static ThemeImportResult Failure(string error)
        => new(false, null, new[] { error }, Array.Empty<string>());

    public static ThemeImportResult Failure(IReadOnlyList<string> errors)
        => new(false, null, errors, Array.Empty<string>());
}
