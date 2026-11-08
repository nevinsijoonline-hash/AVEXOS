using AVEXOS.ThemeEngine;
using Xunit;

namespace AVEXOS.ThemeEngine.Tests;

public class ThemeResourceBuilderTests
{
    [Fact]
    public void Builds_Color_Entries_With_Avex_Prefix()
    {
        var theme = MinimalTheme();
        var resources = ThemeResourceBuilder.Build(theme);

        Assert.Equal("#141517", resources.ColorHex["AvexBgBaseColor"]);
        Assert.Equal("#6E8BFF", resources.ColorHex["AvexAccentColor"]);
    }

    [Fact]
    public void Builds_Brush_Entries_Referencing_Color_Keys()
    {
        var theme = MinimalTheme();
        var resources = ThemeResourceBuilder.Build(theme);

        Assert.Equal("AvexBgBaseColor", resources.BrushSource["AvexBgBaseBrush"]);
        Assert.Equal("AvexAccentColor", resources.BrushSource["AvexAccentBrush"]);
    }

    [Fact]
    public void Converts_Dotted_Color_Keys_To_PascalCase()
    {
        var theme = MinimalTheme();
        var resources = ThemeResourceBuilder.Build(theme);

        Assert.Contains("AvexBorderSubtleColor", resources.ColorHex.Keys);
        Assert.Contains("AvexTextPrimaryColor", resources.ColorHex.Keys);
        Assert.Contains("AvexAccentHoverColor", resources.ColorHex.Keys);
    }

    [Fact]
    public void Builds_Radius_Scalars()
    {
        var theme = MinimalTheme();
        var resources = ThemeResourceBuilder.Build(theme);

        Assert.Equal(2.0, resources.Scalars["AvexRadiusSm"]);
        Assert.Equal(3.0, resources.Scalars["AvexRadiusMd"]);
        Assert.Equal(5.0, resources.Scalars["AvexRadiusLg"]);
    }

    [Fact]
    public void Builds_Space_Scalars()
    {
        var theme = MinimalTheme();
        var resources = ThemeResourceBuilder.Build(theme);

        Assert.Equal(4.0, resources.Scalars["AvexSpaceXs"]);
        Assert.Equal(8.0, resources.Scalars["AvexSpaceSm"]);
    }

    [Fact]
    public void Builds_Motion_Scalars()
    {
        var theme = MinimalTheme();
        var resources = ThemeResourceBuilder.Build(theme);

        Assert.Equal(130.0, resources.Scalars["AvexMotionFast"]);
        Assert.Equal(165.0, resources.Scalars["AvexMotionStandard"]);
    }

    [Fact]
    public void Builds_Type_Scalars()
    {
        var theme = MinimalTheme();
        var resources = ThemeResourceBuilder.Build(theme);

        Assert.Equal(28.0, resources.Scalars["AvexTypeDisplay"]);
        Assert.Equal(13.0, resources.Scalars["AvexTypeBody"]);
    }

    [Fact]
    public void Includes_FontFamily()
    {
        var theme = MinimalTheme();
        var resources = ThemeResourceBuilder.Build(theme);

        Assert.Equal("Segoe UI Variable", resources.FontFamily);
    }

    [Fact]
    public void Color_Count_Matches_Brush_Count()
    {
        var theme = MinimalTheme();
        var resources = ThemeResourceBuilder.Build(theme);

        Assert.Equal(resources.ColorHex.Count, resources.BrushSource.Count);
    }

    [Fact]
    public void Every_Brush_References_A_Valid_Color_Key()
    {
        var theme = MinimalTheme();
        var resources = ThemeResourceBuilder.Build(theme);

        foreach (var kvp in resources.BrushSource)
        {
            Assert.Contains(kvp.Value, resources.ColorHex.Keys);
        }
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
            ["accent.hover"] = "#8099FF",
            ["border.subtle"] = "#2C3137"
        },
        Radius = new Dictionary<string, int> { ["sm"] = 2, ["md"] = 3, ["lg"] = 5 },
        Space = new Dictionary<string, int> { ["xs"] = 4, ["sm"] = 8 },
        Motion = new Dictionary<string, int> { ["fast"] = 130, ["standard"] = 165 },
        Type = new Dictionary<string, int> { ["display"] = 28, ["body"] = 13 },
        Targets = new Dictionary<string, ThemeTarget>
        {
            ["avex.controlcenter"] = new() { Level = "A" }
        }
    };
}
