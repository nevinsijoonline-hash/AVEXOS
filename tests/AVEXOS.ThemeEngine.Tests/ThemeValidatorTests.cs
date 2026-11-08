using AVEXOS.ThemeEngine;
using Xunit;

namespace AVEXOS.ThemeEngine.Tests;

public class ThemeValidatorTests
{
    private readonly ThemeValidator _validator = new();

    [Fact]
    public void BuiltIn_Dark_Theme_Validates()
    {
        var theme = LoadBuiltIn("avex-dark.json");
        var result = _validator.Validate(theme);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void BuiltIn_Light_Theme_Validates()
    {
        var theme = LoadBuiltIn("avex-light.json");
        var result = _validator.Validate(theme);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Rejects_Invalid_Color()
    {
        var theme = MinimalTheme();
        theme.Color["accent"] = "not-a-color";
        var result = _validator.Validate(theme);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("accent"));
    }

    [Fact]
    public void Rejects_Level_D_Target()
    {
        var theme = MinimalTheme();
        theme.Targets["some.unsupported"] = new ThemeTarget { Level = "D" };
        var result = _validator.Validate(theme);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Level D"));
    }

    [Fact]
    public void Rejects_Invalid_Id()
    {
        var theme = MinimalTheme();
        theme.Id = "INVALID ID";
        var result = _validator.Validate(theme);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_Invalid_Version()
    {
        var theme = MinimalTheme();
        theme.Version = "1.0";
        var result = _validator.Validate(theme);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_Invalid_Mode()
    {
        var theme = MinimalTheme();
        theme.Mode = "neon";
        var result = _validator.Validate(theme);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_Negative_Radius()
    {
        var theme = MinimalTheme();
        theme.Radius["sm"] = -5;
        var result = _validator.Validate(theme);
        Assert.False(result.IsValid);
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

    private static ThemeDocument LoadBuiltIn(string name)
    {
        var assembly = typeof(ThemeValidatorTests).Assembly;
        // The built-in themes are embedded in AVEXOS.ThemeEngine, not in the test assembly.
        // Load from the ThemeEngine assembly.
        var themeEngineAssembly = typeof(BuiltInThemeCatalog).Assembly;
        var resourceName = $"AVEXOS.ThemeEngine.Themes.{name}";
        using var stream = themeEngineAssembly.GetManifestResourceStream(resourceName)!;
        return System.Text.Json.JsonSerializer.Deserialize<ThemeDocument>(stream, AVEXOS.Core.AvexJson.Options)!;
    }
}
