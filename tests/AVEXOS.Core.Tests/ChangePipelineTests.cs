using AVEXOS.Core;
using Xunit;

namespace AVEXOS.Core.Tests;

public class ChangePipelineTests
{
    [Fact]
    public async Task Rollback_With_VerifyRollback_False_Reports_Failed()
    {
        var guard = new AllowAllGuard();
        var backups = new InMemoryBackupStore();
        var audit = new InMemoryAuditLog();
        var operations = new InMemoryOperationStore();
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

        var pipeline = new ChangePipeline(guard, backups, audit, operations, clock);

        var request = new ChangeRequest(
            Kind: "test",
            TargetId: "target-1",
            Description: "test change",
            PathToWrite: "",
            Risk: RiskLevel.Low);

        var result = await pipeline.ExecuteAsync(
            request,
            capture: () => "original-state",
            apply: _ => throw new Exception("apply failed"),
            verify: () => true,
            restore: _ => { },
            approve: _ => Task.FromResult(true),
            verifyRollback: () => false);

        Assert.Equal(ChangeState.Failed, result.State);
        Assert.False(result.Succeeded);
        Assert.Contains("Rollback verification failed", result.Detail);
    }

    [Fact]
    public async Task Rollback_With_VerifyRollback_True_Reports_RolledBack()
    {
        var guard = new AllowAllGuard();
        var backups = new InMemoryBackupStore();
        var audit = new InMemoryAuditLog();
        var operations = new InMemoryOperationStore();
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

        var pipeline = new ChangePipeline(guard, backups, audit, operations, clock);

        var request = new ChangeRequest(
            Kind: "test",
            TargetId: "target-1",
            Description: "test change",
            PathToWrite: "",
            Risk: RiskLevel.Low);

        var result = await pipeline.ExecuteAsync(
            request,
            capture: () => "original-state",
            apply: _ => throw new Exception("apply failed"),
            verify: () => true,
            restore: _ => { },
            approve: _ => Task.FromResult(true),
            verifyRollback: () => true);

        Assert.Equal(ChangeState.RolledBack, result.State);
        Assert.False(result.Succeeded);
        Assert.Contains("Original state restored", result.Detail);
    }

    [Fact]
    public async Task Rollback_Without_VerifyRollback_Reports_RolledBack()
    {
        var guard = new AllowAllGuard();
        var backups = new InMemoryBackupStore();
        var audit = new InMemoryAuditLog();
        var operations = new InMemoryOperationStore();
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

        var pipeline = new ChangePipeline(guard, backups, audit, operations, clock);

        var request = new ChangeRequest(
            Kind: "test",
            TargetId: "target-1",
            Description: "test change",
            PathToWrite: "",
            Risk: RiskLevel.Low);

        var result = await pipeline.ExecuteAsync(
            request,
            capture: () => "original-state",
            apply: _ => throw new Exception("apply failed"),
            verify: () => true,
            restore: _ => { },
            approve: _ => Task.FromResult(true));

        Assert.Equal(ChangeState.RolledBack, result.State);
        Assert.False(result.Succeeded);
    }
}

file class AllowAllGuard : IFileSystemGuard
{
    public GuardResult EnsureWritable(string path) => new(true, path, "Allowed");
    public GuardResult EnsureDirectory(string path) => new(true, path, "Allowed");
}

file class InMemoryBackupStore : IBackupStore
{
    public BackupSnapshot Create<T>(string targetId, T state, string description)
        => new($"bk-{Guid.NewGuid():N}", targetId, "test-path", DateTimeOffset.UtcNow, description);
    public T? Load<T>(string snapshotId) where T : class => default;
    public IReadOnlyList<BackupSnapshot> List(int count = 50) => [];
}

file class InMemoryAuditLog : IAuditLog
{
    public List<AuditEntry> Entries { get; } = [];
    public void Append(AuditEntry entry) => Entries.Add(entry);
    public IReadOnlyList<AuditEntry> Recent(int count = 100) => Entries.TakeLast(count).ToList();
}

file class InMemoryOperationStore : IOperationStore
{
    public List<OperationRecord> Records { get; } = [];
    public void Save(OperationRecord record) => Records.Add(record);
    public IReadOnlyList<OperationRecord> Recent(int count = 100) => Records.TakeLast(count).ToList();
    public int CountByState(ChangeState state) => Records.Count(r => r.State == state);
}

file class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; }
    public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
}
