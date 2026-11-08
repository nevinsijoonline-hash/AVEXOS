using System.Collections.Generic;
using AVEXOS.App;
using Xunit;

namespace AVEXOS.App.Tests;

public class ShortcutParserTests
{
    // ---- TryParse --------------------------------------------------------

    [Theory]
    [InlineData("Ctrl+Shift+T", ShortcutParser.ModControl | ShortcutParser.ModShift, 0x54)]
    [InlineData("Ctrl+Shift+W", ShortcutParser.ModControl | ShortcutParser.ModShift, 0x57)]
    [InlineData("Ctrl+Shift+A", ShortcutParser.ModControl | ShortcutParser.ModShift, 0x41)]
    [InlineData("Ctrl+Shift+S", ShortcutParser.ModControl | ShortcutParser.ModShift, 0x53)]
    [InlineData("Alt+F4",         ShortcutParser.ModAlt, 0x73)]
    [InlineData("Ctrl+Alt+Del",   ShortcutParser.ModControl | ShortcutParser.ModAlt, 0x2E)]
    [InlineData("Ctrl+Shift+F1",  ShortcutParser.ModControl | ShortcutParser.ModShift, 0x70)]
    [InlineData("Win+Ctrl+Z",    ShortcutParser.ModWin | ShortcutParser.ModControl, 0x5A)]
    public void TryParse_ValidShortcuts_ReturnsExpectedModifiersAndVk(
        string input, uint expectedMod, byte expectedVk)
    {
        var ok = ShortcutParser.TryParse(input, out var mod, out var vk);
        Assert.True(ok);
        Assert.Equal(expectedMod, mod);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl")]           // no key
    [InlineData("T")]              // no modifier
    [InlineData("Ctrl+")]          // trailing +
    [InlineData("+T")]             // leading +
    public void TryParse_InvalidShortcuts_ReturnsFalse(string input)
    {
        Assert.False(ShortcutParser.TryParse(input, out _, out _));
    }

    // ---- Format ----------------------------------------------------------

    [Theory]
    [InlineData(ShortcutParser.ModControl | ShortcutParser.ModShift, 0x54, "Ctrl+Shift+T")]
    [InlineData(ShortcutParser.ModAlt, 0x73, "Alt+F4")]
    [InlineData(ShortcutParser.ModControl | ShortcutParser.ModShift, 0x70, "Ctrl+Shift+F1")]
    public void Format_RoundTrips(uint mod, byte vk, string expected)
    {
        Assert.Equal(expected, ShortcutParser.Format(mod, vk));
    }

    // ---- IsValid ---------------------------------------------------------

    [Theory]
    [InlineData("Ctrl+Shift+T", true)]
    [InlineData("Alt+F4", true)]
    [InlineData("Ctrl+A", true)]
    [InlineData("Shift+Enter", true)]
    [InlineData("T", false)]            // no modifier
    [InlineData("", false)]
    [InlineData("Ctrl+", false)]
    public void IsValid_ReturnsExpected(string input, bool expected)
    {
        Assert.Equal(expected, ShortcutParser.IsValid(input));
    }

    // ---- IsSystemReserved ------------------------------------------------

    [Theory]
    [InlineData("Alt+Tab", true)]
    [InlineData("Alt+Escape", true)]
    [InlineData("Ctrl+Alt+Delete", true)]
    [InlineData("Win+D", true)]
    [InlineData("Win+E", true)]
    [InlineData("Win+L", true)]
    [InlineData("Win+R", true)]
    [InlineData("Alt+F4", true)]
    [InlineData("Ctrl+Shift+T", false)]
    [InlineData("Ctrl+Shift+W", false)]
    [InlineData("Ctrl+A", false)]
    public void IsSystemReserved_KnownShortcuts(string input, bool expected)
    {
        Assert.Equal(expected, ShortcutParser.IsSystemReserved(input));
    }

    // ---- ConflictsWith ---------------------------------------------------

    [Fact]
    public void ConflictsWith_DetectsDuplicate()
    {
        var existing = new Dictionary<HotkeyAction, string>
        {
            [HotkeyAction.ToggleTheme] = "Ctrl+Shift+T",
            [HotkeyAction.NextWallpaper] = "Ctrl+Shift+W",
        };

        Assert.True(ShortcutParser.ConflictsWith("Ctrl+Shift+T", existing, HotkeyAction.ToggleTheme) is false
            || ShortcutParser.ConflictsWith("Ctrl+Shift+T", existing) is true);

        // Same shortcut, different action → conflict
        Assert.True(ShortcutParser.ConflictsWith("Ctrl+Shift+T", existing, HotkeyAction.OpenPanel));

        // New shortcut → no conflict
        Assert.False(ShortcutParser.ConflictsWith("Ctrl+Shift+X", existing));
    }

    // ---- GetDefaults -----------------------------------------------------

    [Fact]
    public void GetDefaults_ContainsAllActions()
    {
        var defaults = ShortcutParser.GetDefaults();
        Assert.Contains(HotkeyAction.ToggleTheme, defaults.Keys);
        Assert.Contains(HotkeyAction.NextWallpaper, defaults.Keys);
        Assert.Contains(HotkeyAction.OpenPanel, defaults.Keys);
        Assert.Contains(HotkeyAction.OpenSettings, defaults.Keys);
    }

    [Fact]
    public void GetDefaults_AllValid()
    {
        foreach (var (_, shortcut) in ShortcutParser.GetDefaults())
        {
            Assert.True(ShortcutParser.IsValid(shortcut), $"Default '{shortcut}' should be valid");
            Assert.False(ShortcutParser.IsSystemReserved(shortcut), $"Default '{shortcut}' should not be system-reserved");
        }
    }

    // ---- ParseKey edge cases ---------------------------------------------

    [Theory]
    [InlineData("Ctrl+Space", 0x20)]
    [InlineData("Ctrl+Enter", 0x0D)]
    [InlineData("Ctrl+Escape", 0x1B)]
    [InlineData("Ctrl+Delete", 0x2E)]
    [InlineData("Ctrl+NumPad5", 0x65)]
    public void TryParse_SpecialKeys(string input, byte expectedVk)
    {
        Assert.True(ShortcutParser.TryParse(input, out _, out var vk));
        Assert.Equal(expectedVk, vk);
    }

    // ---- Case insensitivity ----------------------------------------------

    [Fact]
    public void TryParse_CaseInsensitive()
    {
        Assert.True(ShortcutParser.TryParse("ctrl+shift+t", out var m1, out var k1));
        Assert.True(ShortcutParser.TryParse("CTRL+SHIFT+T", out var m2, out var k2));
        Assert.Equal(m1, m2);
        Assert.Equal(k1, k2);
    }
}
