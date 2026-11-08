namespace AVEXOS.App;

/// <summary>
/// Identifies a quick action that can be bound to a global hotkey.
/// </summary>
public enum HotkeyAction
{
    ToggleTheme,
    NextWallpaper,
    OpenPanel,
    OpenSettings
}

/// <summary>
/// Pure utility for parsing, formatting, and validating keyboard shortcut strings.
/// Has no Win32 dependencies so it can be unit-tested on any platform.
/// </summary>
public static class ShortcutParser
{
    // ---- defaults --------------------------------------------------------

    public static Dictionary<HotkeyAction, string> GetDefaults() => new()
    {
        [HotkeyAction.ToggleTheme]   = "Ctrl+Shift+T",
        [HotkeyAction.NextWallpaper] = "Ctrl+Shift+W",
        [HotkeyAction.OpenPanel]     = "Ctrl+Shift+A",
        [HotkeyAction.OpenSettings]  = "Ctrl+Shift+S",
    };

    // ---- parsing ---------------------------------------------------------

    /// <summary>
    /// Parse "Ctrl+Shift+T" into (MOD_CONTROL | MOD_SHIFT, VK_T).
    /// Returns false when the string is malformed.
    /// </summary>
    public static bool TryParse(string shortcut, out uint modifiers, out byte vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(shortcut))
            return false;

        var parts = shortcut.Split('+');
        if (parts.Length < 2)
            return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= ParseModifier(parts[i].Trim());
        }

        vk = ParseKey(parts[^1].Trim());
        return modifiers != 0 && vk != 0;
    }

    /// <summary>
    /// Non-throwing variant – returns (0, 0) on bad input.
    /// </summary>
    public static (uint modifiers, byte vk) Parse(string shortcut)
    {
        TryParse(shortcut, out var m, out var k);
        return (m, k);
    }

    // ---- formatting ------------------------------------------------------

    public static string Format(uint modifiers, byte vk)
    {
        var parts = new List<string>();
        if ((modifiers & ModControl) != 0) parts.Add("Ctrl");
        if ((modifiers & ModShift)  != 0) parts.Add("Shift");
        if ((modifiers & ModAlt)    != 0) parts.Add("Alt");
        if ((modifiers & ModWin)    != 0) parts.Add("Win");
        parts.Add(FormatKey(vk));
        return string.Join("+", parts);
    }

    /// <summary>Normalise user input: trim whitespace, title-case modifiers.</summary>
    public static string? TryFormatFromInput(string raw)
    {
        if (TryParse(raw, out var m, out var k))
            return Format(m, k);
        return null;
    }

    // ---- validation ------------------------------------------------------

    /// <summary>
    /// A shortcut must have at least one modifier and a non-modifier key.
    /// </summary>
    public static bool IsValid(string shortcut)
    {
        if (!TryParse(shortcut, out var modifiers, out var vk))
            return false;

        // Must have at least one modifier
        if (modifiers == 0) return false;

        // Must not be ONLY modifiers (vk would be 0 in that case, already caught above)
        if (vk == 0) return false;

        // Reject lone Win-key combos (reserved by shell)
        if ((modifiers & ModWin) != 0 &&
            (modifiers & ~(ModWin | ModControl | ModShift | ModAlt)) == 0 &&
            (modifiers & (ModControl | ModShift | ModAlt)) == 0)
            return false;

        return true;
    }

    /// <summary>
    /// Returns true when <paramref name="shortcut"/> conflicts with a well-known
    /// Windows system hotkey.
    /// </summary>
    public static bool IsSystemReserved(string shortcut)
    {
        if (!TryParse(shortcut, out var mod, out var vk))
            return false;

        // Alt+Tab, Alt+Esc
        if (mod == ModAlt && (vk == VK_TAB || vk == VK_ESCAPE))
            return true;

        // Ctrl+Alt+Del
        if (mod == (ModControl | ModAlt) && vk == VK_DELETE)
            return true;

        // Win+D, Win+E, Win+L, Win+R, Win+I  (shell hotkeys)
        if ((mod & ModWin) != 0)
        {
            if (vk is VK_D or VK_E or VK_L or VK_R or VK_I)
                return true;
        }

        // Alt+F4
        if (mod == ModAlt && vk == VK_F4)
            return true;

        return false;
    }

    /// <summary>
    /// Check whether <paramref name="candidate"/> clashes with any entry in
    /// <paramref name="existing"/> (excluding the entry for <paramref name="excludeAction"/>).
    /// </summary>
    public static bool ConflictsWith(
        string candidate,
        IReadOnlyDictionary<HotkeyAction, string> existing,
        HotkeyAction excludeAction = (HotkeyAction)(-1))
    {
        foreach (var kvp in existing)
        {
            if (kvp.Key == excludeAction) continue;
            if (string.Equals(kvp.Value, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ---- modifier constants (match Win32 MOD_*) --------------------------

    public const uint ModAlt     = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift   = 0x0004;
    public const uint ModWin     = 0x0008;

    // ---- VK constants ----------------------------------------------------

    private const byte VK_TAB       = 0x09;
    private const byte VK_ESCAPE    = 0x1B;
    private const byte VK_DELETE    = 0x2E;
    private const byte VK_F4        = 0x73;
    private const byte VK_D         = 0x44;
    private const byte VK_E         = 0x45;
    private const byte VK_I         = 0x49;
    private const byte VK_L         = 0x4C;
    private const byte VK_R         = 0x52;
    private const byte VK_T         = 0x54;
    private const byte VK_W         = 0x57;
    private const byte VK_A         = 0x41;
    private const byte VK_S         = 0x53;
    private const byte VK_F1        = 0x70;
    private const byte VK_F12       = 0x7B;
    private const byte VK_NUMPAD0   = 0x60;
    private const byte VK_NUMPAD9   = 0x69;
    private const byte VK_0         = 0x30;
    private const byte VK_9         = 0x39;

    // ---- internal helpers ------------------------------------------------

    private static uint ParseModifier(string s) => s.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => ModControl,
        "SHIFT"             => ModShift,
        "ALT"               => ModAlt,
        "WIN" or "WINDOWS" or "META" => ModWin,
        _ => 0
    };

    internal static byte ParseKey(string s)
    {
        var upper = s.ToUpperInvariant();

        // Single letter A-Z  (ASCII 0x41-0x5A matches VK_A-VK_Z)
        if (upper.Length == 1 && char.IsLetter(upper[0]))
            return (byte)upper[0];

        // Digit 0-9
        if (upper.Length == 1 && char.IsDigit(upper[0]))
            return (byte)(upper[0] - '0' + VK_0);

        // F1-F24
        if (upper.StartsWith('F') && int.TryParse(upper[1..], out var fNum) && fNum is >= 1 and <= 24)
            return (byte)(VK_F1 + fNum - 1);

        return upper switch
        {
            "SPACE"      => 0x20,
            "ENTER"      => 0x0D,
            "TAB"        => VK_TAB,
            "ESCAPE" or "ESC" => VK_ESCAPE,
            "BACKSPACE"  => 0x08,
            "DELETE" or "DEL" => VK_DELETE,
            "INSERT" or "INS" => 0x2D,
            "HOME"       => 0x24,
            "END"        => 0x23,
            "PAGEUP" or "PGUP"   => 0x21,
            "PAGEDOWN" or "PGDN" => 0x22,
            "UP"         => 0x26,
            "DOWN"       => 0x28,
            "LEFT"       => 0x25,
            "RIGHT"      => 0x27,
            "NUMPAD0"    => VK_NUMPAD0,
            "NUMPAD1"    => 0x61,
            "NUMPAD2"    => 0x62,
            "NUMPAD3"    => 0x63,
            "NUMPAD4"    => 0x64,
            "NUMPAD5"    => 0x65,
            "NUMPAD6"    => 0x66,
            "NUMPAD7"    => 0x67,
            "NUMPAD8"    => 0x68,
            "NUMPAD9"    => VK_NUMPAD9,
            _ => 0
        };
    }

    private static string FormatKey(byte vk)
    {
        // A-Z
        if (vk >= 0x41 && vk <= 0x5A)
            return ((char)vk).ToString();

        // 0-9
        if (vk >= VK_0 && vk <= VK_9)
            return ((char)(vk - VK_0 + '0')).ToString();

        // F1-F24
        if (vk >= VK_F1 && vk <= VK_F12 + 12)
            return $"F{vk - VK_F1 + 1}";

        // Numpad 0-9
        if (vk >= VK_NUMPAD0 && vk <= VK_NUMPAD9)
            return $"NumPad{vk - VK_NUMPAD0}";

        return vk switch
        {
            0x20 => "Space",
            0x0D => "Enter",
            0x09 => "Tab",
            0x1B => "Escape",
            0x08 => "Backspace",
            0x2E => "Delete",
            0x2D => "Insert",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x26 => "Up",
            0x28 => "Down",
            0x25 => "Left",
            0x27 => "Right",
            _ => $"VK_{vk:X2}"
        };
    }
}
