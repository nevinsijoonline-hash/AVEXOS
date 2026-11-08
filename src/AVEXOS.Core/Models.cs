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

[JsonSourceGenerationOptions(WriteIndented = true)]
public sealed record CapabilitySummary(int Total, int LevelA, int LevelB, int LevelC, int LevelD);
