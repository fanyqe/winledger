namespace WinLedger.Domain.Rollback;

public sealed record EnvironmentRollbackResult(
    Guid OperationId,
    bool Succeeded,
    RollbackValidationState ValidationState,
    string Message);
