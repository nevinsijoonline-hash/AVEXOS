namespace AVEXOS.Core;

public interface IPathPolicy
{
    /// <summary>Resolves a path to its canonical, reparse-free form.</summary>
    string Canonicalize(string path);

    /// <summary>Deny-by-default check against protected and allowed roots.</summary>
    bool IsAllowed(string canonicalPath, out string reason);
}

public interface IFileSystemGuard
{
    GuardResult EnsureWritable(string path);
    GuardResult EnsureDirectory(string path);
}

public interface IWallpaperService
{
    AppearanceState Read();
    void Apply(AppearanceState state);
    string ImportWallpaper(string sourcePath);
}

public interface IAppDiscoveryService
{
    Task<IReadOnlyList<DiscoveredApplication>> DiscoverAsync(CancellationToken cancellationToken = default);
}

public interface IBackupStore
{
    BackupSnapshot Create<T>(string targetId, T payload, string description);
    T? Load<T>(string snapshotId) where T : class;
    IReadOnlyList<BackupSnapshot> List(int count = 50);
}

public interface IAuditLog
{
    void Append(AuditEntry entry);
    IReadOnlyList<AuditEntry> Recent(int count = 100);
}

public interface IOperationStore
{
    void Save(OperationRecord record);
    IReadOnlyList<OperationRecord> Recent(int count = 100);
    int CountByState(ChangeState state);
}

public interface ISettingsStore
{
    string? Get(string key);
    void Set(string key, string value);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
