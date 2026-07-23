namespace WinLedger.Domain.Rollback;

public sealed record ScheduledTaskRollbackResult(
    Guid OperationId,
    bool Succeeded,
    RollbackValidationState ValidationState,
    string Message);
