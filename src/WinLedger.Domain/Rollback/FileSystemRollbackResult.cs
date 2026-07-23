namespace WinLedger.Domain.Rollback;

public sealed record FileSystemRollbackResult(
    Guid OperationId,
    bool Succeeded,
    RollbackValidationState ValidationState,
    string Message);
