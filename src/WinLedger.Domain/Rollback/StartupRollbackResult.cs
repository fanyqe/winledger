namespace WinLedger.Domain.Rollback;

public sealed record StartupRollbackResult(
    Guid OperationId,
    bool Succeeded,
    RollbackValidationState ValidationState,
    string Message);
