namespace WinLedger.Domain.Rollback;

public sealed record ServiceRollbackResult(
    Guid OperationId,
    bool Succeeded,
    RollbackValidationState ValidationState,
    string Message);
