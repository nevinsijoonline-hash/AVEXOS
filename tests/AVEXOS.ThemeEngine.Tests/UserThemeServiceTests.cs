using System.Text.Json;
using AVEXOS.Core;
using AVEXOS.ThemeEngine;
using Xunit;

namespace AVEXOS.ThemeEngine.Tests;

public class UserThemeServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly UserThemeService _service;
    private readonly ThemeValidator _validator;

    public UserThemeServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"avex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        // Override paths for testing
        AvexPaths.OverrideThemeDirForTests(_testDir);
        _validator = new ThemeValidator();
        _service = new UserThemeService(_validator);
    }

    public void Dispose()
    {
        AvexPaths.OverrideThemeDirForTests(null!);
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void Export_Creates_Valid_Json_File()
    {
        var theme = MinimalTheme();
        var filePath = Path.Combine(_testDir, "export-test.json");

        _service.Export(theme, filePath);

        Assert.True(File.Exists(filePath));
        var json = File.ReadAllText(filePath);
        var deserialized = JsonSerializer.Deserialize<ThemeDocument>(json, AvexJson.Options);
        Assert.NotNull(deserialized);
        Assert.Equal(theme.Id, deserialized!.Id);
        Assert.Equal(theme.Name, deserialized.Name);
    }

    [Fact]
    public void Import_Valid_Theme_Saves_To_User_Dir()
    {
        var theme = MinimalTheme();
        var filePath = Path.Combine(_testDir, "import-source.json");
        File.WriteAllText(filePath, JsonSerializer.Serialize(theme, AvexJson.Options));

        var result = _service.Import(filePath);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Theme);
        Assert.Equal(theme.Id, result.Theme!.Id);

        // Verify file was saved to user themes dir
        var userFile = Path.Combine(UserThemeService.UserThemesDir, $"{theme.Id}.json");
        Assert.True(File.Exists(userFile));
    }

    [Fact]
    public void Import_Invalid_Theme_Returns_Errors()
    {
        var theme = MinimalTheme();
        theme.Id = "INVALID";
        var filePath = Path.Combine(_testDir, "bad-theme.json");
        File.WriteAllText(filePath, JsonSerializer.Serialize(theme, AvexJson.Options));

        var result = _service.Import(filePath);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Import_Missing_File_Returns_Error()
    {
        var result = _service.Import(Path.Combine(_testDir, "nonexistent.json"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("not found"));
    }

    [Fact]
    public void Import_Invalid_Json_Returns_Error()
    {
        var filePath = Path.Combine(_testDir, "not-json.json");
        File.WriteAllText(filePath, "this is not json {{{");

        var result = _service.Import(filePath);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Delete_Removes_User_Theme_File()
    {
        // First import a theme
        var theme = MinimalTheme();
        var filePath = Path.Combine(_testDir, "to-delete.json");
        File.WriteAllText(filePath, JsonSerializer.Serialize(theme, AvexJson.Options));
        _service.Import(filePath);

        // Now delete it
        var deleted = _service.Delete(theme.Id);
        Assert.True(deleted);

        // Verify file is gone
        var userFile = Path.Combine(UserThemeService.UserThemesDir, $"{theme.Id}.json");
        Assert.False(File.Exists(userFile));
    }

    [Fact]
    public void Delete_Nonexistent_Returns_False()
    {
        var deleted = _service.Delete("nonexistent.theme");
        Assert.False(deleted);
    }

    [Fact]
    public void LoadAll_Returns_Imported_Themes()
    {
        var theme1 = MinimalTheme();
        theme1.Id = "test.alpha";
        theme1.Name = "Alpha";

        var theme2 = MinimalTheme();
        theme2.Id = "test.beta";
        theme2.Name = "Beta";

        // Import both
        var file1 = Path.Combine(_testDir, "alpha.json");
        var file2 = Path.Combine(_testDir, "beta.json");
        File.WriteAllText(file1, JsonSerializer.Serialize(theme1, AvexJson.Options));
        File.WriteAllText(file2, JsonSerializer.Serialize(theme2, AvexJson.Options));
        _service.Import(file1);
        _service.Import(file2);

        var loaded = _service.LoadAll();
        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, t => t.Id == "test.alpha");
        Assert.Contains(loaded, t => t.Id == "test.beta");
    }

    [Fact]
    public void LoadAll_Empty_When_No_User_Dir()
    {
        var loaded = _service.LoadAll();
        Assert.Empty(loaded);
    }

    [Fact]
    public void Export_Then_Import_Roundtrip()
    {
        var original = MinimalTheme();
        var exportPath = Path.Combine(_testDir, "roundtrip.json");

        // Export
        _service.Export(original, exportPath);

        // Import
        var result = _service.Import(exportPath);
        Assert.True(result.IsSuccess);
        Assert.Equal(original.Id, result.Theme!.Id);
        Assert.Equal(original.Name, result.Theme.Name);
        Assert.Equal(original.Mode, result.Theme.Mode);
        Assert.Equal(original.Color.Count, result.Theme.Color.Count);
    }

    private static ThemeDocument MinimalTheme() => new()
    {
        Id = "test.minimal",
        Name = "Minimal",
        Version = "1.0.0",
        Mode = "dark",
        FontFamily = "Segoe UI Variable",
        Color = new Dictionary<string, string>
        {
            ["bg.base"] = "#141517",
            ["bg.raised"] = "#191B1E",
            ["surface.1"] = "#1E2125",
            ["text.primary"] = "#ECEDEF",
            ["text.secondary"] = "#A2A9B4",
            ["accent"] = "#6E8BFF",
            ["border.subtle"] = "#2C3137"
        },
        Radius = new Dictionary<string, int> { ["sm"] = 2, ["md"] = 3, ["lg"] = 5 },
        Space = new Dictionary<string, int> { ["xs"] = 4, ["sm"] = 8 },
        Motion = new Dictionary<string, int> { ["fast"] = 130 },
        Type = new Dictionary<string, int> { ["body"] = 13 },
        Targets = new Dictionary<string, ThemeTarget>
        {
            ["avex.controlcenter"] = new() { Level = "A" }
        }
    };
}
