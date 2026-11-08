using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace AVEXOS.App.Tests;

/// <summary>
/// Static verification tests for UI structure.
/// These tests verify XAML files exist, are well-formed, and navigation is consistent
/// without requiring a running UI thread or WinUI dependencies.
/// </summary>
public class UiStructureTests
{
    private static readonly string AppProjectRoot;
    private static readonly string ViewsDir;

    static UiStructureTests()
    {
        // Find solution root by looking for .slnx file
        var current = AppContext.BaseDirectory;
        while (current != null && !File.Exists(Path.Combine(current, "AVEXOS.slnx")))
        {
            current = Path.GetDirectoryName(current);
        }
        
        if (current == null)
        {
            throw new InvalidOperationException("Could not find solution root (AVEXOS.slnx)");
        }
        
        AppProjectRoot = Path.Combine(current, "src", "AVEXOS.App");
        ViewsDir = Path.Combine(AppProjectRoot, "Views");
    }

    [Fact]
    public void MainWindow_Xaml_Exists()
    {
        var path = Path.Combine(AppProjectRoot, "MainWindow.xaml");
        Assert.True(File.Exists(path), $"MainWindow.xaml not found at {path}");
    }

    [Fact]
    public void App_Xaml_Exists()
    {
        var path = Path.Combine(AppProjectRoot, "App.xaml");
        Assert.True(File.Exists(path), $"App.xaml not found at {path}");
    }

    [Theory]
    [InlineData("HomePage.xaml")]
    [InlineData("AppearancePage.xaml")]
    [InlineData("ThemesPage.xaml")]
    [InlineData("ApplicationsPage.xaml")]
    [InlineData("ActivityPage.xaml")]
    [InlineData("QuickActionsPage.xaml")]
    [InlineData("WorkspaceProfilesPage.xaml")]
    [InlineData("SettingsPage.xaml")]
    public void Page_Xaml_Exists(string fileName)
    {
        var path = Path.Combine(ViewsDir, fileName);
        Assert.True(File.Exists(path), $"Page XAML not found: {path}");
    }

    [Theory]
    [InlineData("HomePage.xaml.cs")]
    [InlineData("AppearancePage.xaml.cs")]
    [InlineData("ThemesPage.xaml.cs")]
    [InlineData("ApplicationsPage.xaml.cs")]
    [InlineData("ActivityPage.xaml.cs")]
    [InlineData("QuickActionsPage.xaml.cs")]
    [InlineData("WorkspaceProfilesPage.xaml.cs")]
    [InlineData("SettingsPage.xaml.cs")]
    public void Page_CodeBehind_Exists(string fileName)
    {
        var path = Path.Combine(ViewsDir, fileName);
        Assert.True(File.Exists(path), $"Code-behind not found: {path}");
    }

    [Fact]
    public void MainWindow_Xaml_IsWellFormed()
    {
        var path = Path.Combine(AppProjectRoot, "MainWindow.xaml");
        var doc = XDocument.Load(path);
        Assert.NotNull(doc.Root);
        Assert.Equal("Window", doc.Root.Name.LocalName);
    }

    [Theory]
    [InlineData("HomePage.xaml")]
    [InlineData("AppearancePage.xaml")]
    [InlineData("ThemesPage.xaml")]
    [InlineData("ApplicationsPage.xaml")]
    [InlineData("ActivityPage.xaml")]
    [InlineData("QuickActionsPage.xaml")]
    [InlineData("WorkspaceProfilesPage.xaml")]
    [InlineData("SettingsPage.xaml")]
    public void Page_Xaml_IsWellFormed(string fileName)
    {
        var path = Path.Combine(ViewsDir, fileName);
        var doc = XDocument.Load(path);
        Assert.NotNull(doc.Root);
        Assert.Equal("Page", doc.Root.Name.LocalName);
    }

    [Fact]
    public void MainWindow_HasExpectedNavigationItems()
    {
        var path = Path.Combine(AppProjectRoot, "MainWindow.xaml");
        var doc = XDocument.Load(path);
        var xns = doc.Root!.GetDefaultNamespace();

        var navItems = doc.Descendants(xns + "NavigationViewItem")
            .Select(e => e.Attribute("Tag")?.Value)
            .Where(v => v is not null)
            .ToList();

        var expected = new[] { "Home", "Appearance", "Themes", "Applications", "Activity", "QuickActions", "WorkspaceProfiles", "Settings" };

        foreach (var tag in expected)
        {
            Assert.Contains(tag, navItems!);
        }
    }

    [Fact]
    public void MainWindow_NavigationItems_HaveIcons()
    {
        var path = Path.Combine(AppProjectRoot, "MainWindow.xaml");
        var doc = XDocument.Load(path);
        var xns = doc.Root!.GetDefaultNamespace();

        var navItems = doc.Descendants(xns + "NavigationViewItem").ToList();

        foreach (var item in navItems)
        {
            var tag = item.Attribute("Tag")?.Value ?? "(untagged)";
            var icon = item.Descendants(xns + "FontIcon").FirstOrDefault();
            Assert.NotNull(icon);
            var glyph = icon.Attribute("Glyph")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(glyph), $"NavigationViewItem '{tag}' has empty Glyph");
        }
    }

    [Fact]
    public void MainWindow_HasContentFrame()
    {
        var path = Path.Combine(AppProjectRoot, "MainWindow.xaml");
        var doc = XDocument.Load(path);
        var xns = doc.Root!.GetDefaultNamespace();
        var xamlNs = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        var frame = doc.Descendants(xns + "Frame")
            .FirstOrDefault(f => f.Attribute(xamlNs + "Name")?.Value == "ContentFrame");

        Assert.NotNull(frame);
    }

    [Theory]
    [InlineData("HomePage.xaml", "AVEXOS.App.HomePage")]
    [InlineData("AppearancePage.xaml", "AVEXOS.App.AppearancePage")]
    [InlineData("ThemesPage.xaml", "AVEXOS.App.ThemesPage")]
    [InlineData("ApplicationsPage.xaml", "AVEXOS.App.ApplicationsPage")]
    [InlineData("ActivityPage.xaml", "AVEXOS.App.ActivityPage")]
    [InlineData("QuickActionsPage.xaml", "AVEXOS.App.QuickActionsPage")]
    [InlineData("WorkspaceProfilesPage.xaml", "AVEXOS.App.WorkspaceProfilesPage")]
    [InlineData("SettingsPage.xaml", "AVEXOS.App.SettingsPage")]
    public void Page_Xaml_HasCorrectClass(string fileName, string expectedClass)
    {
        var path = Path.Combine(ViewsDir, fileName);
        var doc = XDocument.Load(path);
        var xClass = doc.Root!.Attribute("{http://schemas.microsoft.com/winfx/2006/xaml}Class")?.Value;
        Assert.Equal(expectedClass, xClass);
    }

    [Fact]
    public void App_Xaml_HasResourceDictionary()
    {
        var path = Path.Combine(AppProjectRoot, "App.xaml");
        var doc = XDocument.Load(path);
        var xns = doc.Root!.GetDefaultNamespace();

        var resourceDict = doc.Descendants(xns + "ResourceDictionary").FirstOrDefault();
        Assert.NotNull(resourceDict);
    }

    [Fact]
    public void AvexTokens_Stylesheet_Exists()
    {
        var path = Path.Combine(AppProjectRoot, "Styles", "AvexTokens.xaml");
        Assert.True(File.Exists(path), $"AvexTokens.xaml not found at {path}");
    }

    [Fact]
    public void AvexTokens_IsWellFormed()
    {
        var path = Path.Combine(AppProjectRoot, "Styles", "AvexTokens.xaml");
        var doc = XDocument.Load(path);
        Assert.NotNull(doc.Root);
    }

    [Fact]
    public void AllPages_HaveLoadedEventHandler()
    {
        var pages = new[]
        {
            "HomePage.xaml.cs",
            "AppearancePage.xaml.cs",
            "ThemesPage.xaml.cs",
            "ApplicationsPage.xaml.cs",
            "ActivityPage.xaml.cs",
            "QuickActionsPage.xaml.cs",
            "WorkspaceProfilesPage.xaml.cs",
            "SettingsPage.xaml.cs"
        };

        foreach (var page in pages)
        {
            var path = Path.Combine(ViewsDir, page);
            var content = File.ReadAllText(path);
            Assert.Contains("Loaded", content, StringComparison.Ordinal);
        }
    }
}
