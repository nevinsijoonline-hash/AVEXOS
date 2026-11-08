using System.Text.Json.Serialization;

namespace AVEXOS.Core;

/// <summary>Capability levels from the master specification (Part 11.1).</summary>
public enum CapabilityLevel
{
    A,
    B,
    C,
    D
}

public enum RiskLevel
{
    Low,
    Medium,
    High
}

public enum ChangeState
{
    Pending,
    Denied,
    Applied,
    Verified,
    RolledBack,
    Failed
}

public sealed record AppearanceState(string WallpaperPath, int WallpaperStyle, bool TileWallpaper);

/// <summary>
/// Represents a detected display monitor.
/// </summary>
public sealed record MonitorInfo(
    string DeviceId,
    string DisplayName,
    int Width,
    int Height,
    int Left,
    int Top,
    bool IsPrimary);

/// <summary>
/// Per-monitor wallpaper state including the monitor and its assigned wallpaper.
/// </summary>
public sealed record MonitorWallpaperState(
    MonitorInfo Monitor,
    string WallpaperPath,
    int WallpaperStyle,
    bool TileWallpaper);

/// <summary>
/// Slideshow configuration for a specific monitor.
/// </summary>
public sealed record MonitorSlideshowConfig(
    string MonitorDeviceId,
    IReadOnlyList<string> Paths,
    int IntervalMinutes,
    int CurrentIndex,
    bool Enabled);

public sealed record DiscoveredApplication(
    string Id,
    string DisplayName,
    string Publisher,
    string Version,
    string InstallPath,
    string Type,
    string Source,
    CapabilityLevel Capability,
    string CapabilityReason);

public sealed record BackupSnapshot(
    string Id,
    string TargetId,
    string PayloadPath,
    DateTimeOffset CreatedUtc,
    string Description);

public sealed record AuditEntry(
    DateTimeOffset Utc,
    string Category,
    string TargetId,
    string? OperationId,
    string Outcome,
    string Detail);

public sealed record OperationRecord(
    string Id,
    string TargetId,
    string Kind,
    RiskLevel Risk,
    ChangeState State,
    DateTimeOffset UpdatedUtc,
    string? Detail);

public sealed record ChangeResult(
    ChangeState State,
    string TargetId,
    bool Verified,
    string Detail)
{
    public bool Succeeded => State is ChangeState.Applied or ChangeState.Verified;
}

public sealed record ChangeRequest(
    string TargetId,
    string Kind,
    string Description,
    RiskLevel Risk,
    string? PathToWrite = null);

public sealed record GuardResult(bool Allowed, string CanonicalPath, string Reason)
{
    public static GuardResult Allow(string path) => new(true, path, "Allowed");
    public static GuardResult Deny(string path, string reason) => new(false, path, reason);
}

/// <summary>
/// Configuration for Presentation Mode: blanks secondary monitors with a solid color
/// while keeping the primary monitor active with its wallpaper.
/// </summary>
public sealed record PresentationModeConfig(
    bool IsEnabled,
    string BlankColor,
    bool BlankPrimaryToo)
{
    /// <summary>Default config: disabled, black, primary monitor excluded.</summary>
    public static PresentationModeConfig Default => new(false, "#000000", false);
}

/// <summary>
/// Saved per-monitor wallpaper state used to restore after presentation mode ends.
/// </summary>
public sealed record PresentationModeSavedState(
    IReadOnlyDictionary<string, string> MonitorWallpapers);

[JsonSourceGenerationOptions(WriteIndented = true)]
public sealed record CapabilitySummary(int Total, int LevelA, int LevelB, int LevelC, int LevelD);

/// <summary>
/// Configuration for Focus Mode: dims non-primary monitors to reduce distractions,
/// optionally simplifying wallpaper to a solid color or blurred version.
/// </summary>
public sealed class FocusModeConfig
{
    public bool IsEnabled { get; set; }
    public bool DimNonPrimary { get; set; } = true;
    public int DimLevel { get; set; } = 50;
    public bool UseSolidColor { get; set; }
    public string SolidColor { get; set; } = "#000000";
    public bool BlurWallpaper { get; set; }
    public int BlurRadius { get; set; } = 10;

    /// <summary>Clamps all values to their valid ranges.</summary>
    public void Validate()
    {
        DimLevel = Math.Clamp(DimLevel, 0, 100);
        BlurRadius = Math.Clamp(BlurRadius, 0, 100);
        if (string.IsNullOrWhiteSpace(SolidColor))
            SolidColor = "#000000";
    }

    /// <summary>Default config: disabled, dim level 50, solid color off.</summary>
    public static FocusModeConfig Default => new()
    {
        IsEnabled = false,
        DimNonPrimary = true,
        DimLevel = 50,
        UseSolidColor = false,
        SolidColor = "#000000",
        BlurWallpaper = false,
        BlurRadius = 10,
    };
}

/// <summary>
/// Saved per-monitor wallpaper state used to restore after focus mode ends.
/// </summary>
public sealed record FocusModeSavedState(
    IReadOnlyDictionary<string, string> MonitorWallpapers);

/// <summary>
/// A saved workspace profile capturing wallpaper, theme, and layout preset.
/// </summary>
public sealed record WorkspaceProfile(
    string Id,
    string Name,
    string WallpaperPath,
    string ThemeId,
    string ThemeMode,
    DateTimeOffset CreatedUtc,
    bool IsDefault);

/// <summary>
/// Configuration for the auto-brightness feature.
/// Brightness values are 0-100. Times are TimeSpan from midnight.
/// </summary>
public sealed class AutoBrightnessConfig
{
    public bool IsEnabled { get; set; }
    public int DayBrightness { get; set; } = 80;
    public int NightBrightness { get; set; } = 30;
    public TimeSpan DayStartTime { get; set; } = new(7, 0, 0);
    public TimeSpan NightStartTime { get; set; } = new(19, 0, 0);
    public int TransitionMinutes { get; set; } = 30;

    /// <summary>Clamps all values to their valid ranges.</summary>
    public void Validate()
    {
        DayBrightness = Math.Clamp(DayBrightness, 0, 100);
        NightBrightness = Math.Clamp(NightBrightness, 0, 100);
        TransitionMinutes = Math.Clamp(TransitionMinutes, 0, 60);
    }
}
