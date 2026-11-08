namespace AVEXOS.Core;

/// <summary>
/// The only mutating entry point in AVEX OS. Every change runs the same
/// ordered stages defined in docs/architecture/ARCHITECTURE.md §5.
/// </summary>
public sealed class ChangePipeline
{
    private readonly IFileSystemGuard _guard;
    private readonly IBackupStore _backups;
    private readonly IAuditLog _audit;
    private readonly IOperationStore _operations;
    private readonly IClock _clock;

    public ChangePipeline(
        IFileSystemGuard guard,
        IBackupStore backups,
        IAuditLog audit,
        IOperationStore operations,
        IClock clock)
    {
        _guard = guard;
        _backups = backups;
        _audit = audit;
        _operations = operations;
        _clock = clock;
    }

    public async Task<ChangeResult> ExecuteAsync<TState>(
        ChangeRequest request,
        Func<TState> capture,
        Action<TState> apply,
        Func<bool> verify,
        Action<TState> restore,
        Func<ChangeRequest, Task<bool>> approve,
        Func<bool>? verifyRollback = null,
        Func<ChangeRequest, Task<bool>>? confirmHighRisk = null)
    {
        var operationId = $"op-{_clock.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";

        // Stage 4 — guard, before anything is read or written.
        if (request.PathToWrite is { Length: > 0 } path)
        {
            var guard = _guard.EnsureWritable(path);
            if (!guard.Allowed)
            {
                Deny(request, operationId, $"Blocked: {guard.Reason}");
                return new ChangeResult(ChangeState.Denied, request.TargetId, false, $"Blocked: {guard.Reason}");
            }
        }

        // Stage 2 — capture the original state.
        TState original;
        try
        {
            original = capture();
        }
        catch (Exception ex)
        {
            Deny(request, operationId, $"Could not read current state: {ex.Message}");
            return new ChangeResult(ChangeState.Failed, request.TargetId, false, ex.Message);
        }

        // Stage 6 — back up before touching anything.
        BackupSnapshot? snapshot;
        try
        {
            snapshot = _backups.Create(request.TargetId, original, request.Description);
        }
        catch (Exception ex)
        {
            Deny(request, operationId, $"Backup failed, change aborted: {ex.Message}");
            return new ChangeResult(ChangeState.Failed, request.TargetId, false, $"Backup failed: {ex.Message}");
        }

        // Stage 7/8 — record and request approval.
        _operations.Save(new OperationRecord(operationId, request.TargetId, request.Kind,
            request.Risk, ChangeState.Pending, _clock.UtcNow, request.Description));

        // Awaited without ConfigureAwait(false): apply/verify/restore below are
        // apartment-sensitive (SystemParametersInfo, registry), so they must stay
        // on the calling UI thread.
        if (!await approve(request))
        {
            _operations.Save(new OperationRecord(operationId, request.TargetId, request.Kind,
                request.Risk, ChangeState.Denied, _clock.UtcNow, "Declined by user"));
            _audit.Append(new AuditEntry(_clock.UtcNow, request.Kind, request.TargetId,
                operationId, "Denied", "Declined by user; no change made."));
            return new ChangeResult(ChangeState.Denied, request.TargetId, false, "Declined by user.");
        }

        // Stage 8b — High-risk changes require explicit second confirmation.
        if (request.Risk == RiskLevel.High)
        {
            if (confirmHighRisk is null)
            {
                _operations.Save(new OperationRecord(operationId, request.TargetId, request.Kind,
                    request.Risk, ChangeState.Denied, _clock.UtcNow,
                    "High-risk change requires confirmHighRisk delegate."));
                _audit.Append(new AuditEntry(_clock.UtcNow, request.Kind, request.TargetId,
                    operationId, "Denied", "High-risk change denied: no confirmHighRisk delegate provided."));
                return new ChangeResult(ChangeState.Denied, request.TargetId, false,
                    "High-risk change requires explicit confirmation. Provide confirmHighRisk delegate.");
            }

            if (!await confirmHighRisk(request))
            {
                _operations.Save(new OperationRecord(operationId, request.TargetId, request.Kind,
                    request.Risk, ChangeState.Denied, _clock.UtcNow, "High-risk confirmation declined."));
                _audit.Append(new AuditEntry(_clock.UtcNow, request.Kind, request.TargetId,
                    operationId, "Denied", "High-risk confirmation declined by user."));
                return new ChangeResult(ChangeState.Denied, request.TargetId, false,
                    "High-risk change declined.");
            }
        }

        // Stage 9 — apply.
        try
        {
            apply(original);
        }
        catch (Exception ex)
        {
            return Rollback(request, operationId, original, restore, verifyRollback, $"Apply failed: {ex.Message}");
        }

        // Stage 10 — verify by reading the value back.
        bool verified;
        try
        {
            verified = verify();
        }
        catch (Exception ex)
        {
            return Rollback(request, operationId, original, restore, verifyRollback, $"Verification error: {ex.Message}");
        }

        if (!verified)
        {
            return Rollback(request, operationId, original, restore, verifyRollback, "Value did not read back as applied.");
        }

        // Stages 11/12 — record success.
        _operations.Save(new OperationRecord(operationId, request.TargetId, request.Kind,
            request.Risk, ChangeState.Verified, _clock.UtcNow,
            $"Verified. Backup {snapshot!.Id}"));
        _audit.Append(new AuditEntry(_clock.UtcNow, request.Kind, request.TargetId, operationId,
            "Verified", $"Verified. Backup {snapshot.Id}."));

        return new ChangeResult(ChangeState.Verified, request.TargetId, true,
            $"Verified. Backup {snapshot.Id}.");
    }

    private ChangeResult Rollback<TState>(
        ChangeRequest request, string operationId, TState original,
        Action<TState> restore, Func<bool>? verifyRollback, string detail)
    {
        try
        {
            restore(original);
            
            // Verify the rollback actually restored the original state.
            if (verifyRollback != null && !verifyRollback())
            {
                _operations.Save(new OperationRecord(operationId, request.TargetId, request.Kind,
                    request.Risk, ChangeState.Failed, _clock.UtcNow,
                    $"{detail} Rollback verification failed."));
                _audit.Append(new AuditEntry(_clock.UtcNow, request.Kind, request.TargetId, operationId,
                    "Failed", $"{detail} Rollback verification failed."));
                return new ChangeResult(ChangeState.Failed, request.TargetId, false,
                    $"{detail} Rollback verification failed.");
            }
            
            _operations.Save(new OperationRecord(operationId, request.TargetId, request.Kind,
                request.Risk, ChangeState.RolledBack, _clock.UtcNow, detail));
            _audit.Append(new AuditEntry(_clock.UtcNow, request.Kind, request.TargetId, operationId,
                "RolledBack", detail));
            return new ChangeResult(ChangeState.RolledBack, request.TargetId, false,
                $"{detail} Original state restored.");
        }
        catch (Exception restoreEx)
        {
            _operations.Save(new OperationRecord(operationId, request.TargetId, request.Kind,
                request.Risk, ChangeState.Failed, _clock.UtcNow,
                $"{detail} Rollback failed: {restoreEx.Message}"));
            _audit.Append(new AuditEntry(_clock.UtcNow, request.Kind, request.TargetId, operationId,
                "Failed", $"{detail} Rollback failed: {restoreEx.Message}"));
            return new ChangeResult(ChangeState.Failed, request.TargetId, false,
                $"{detail} Rollback failed: {restoreEx.Message}");
        }
    }

    private void Deny(ChangeRequest request, string operationId, string detail)
    {
        _operations.Save(new OperationRecord(operationId, request.TargetId, request.Kind,
            request.Risk, ChangeState.Denied, _clock.UtcNow, detail));
        _audit.Append(new AuditEntry(_clock.UtcNow, request.Kind, request.TargetId, operationId,
            "Denied", detail));
    }
}
