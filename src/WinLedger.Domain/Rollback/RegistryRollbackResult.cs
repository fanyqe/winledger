namespace WinLedger.Domain.Rollback;

public sealed record RegistryRollbackResult(
    Guid OperationId,
    bool Succeeded,
    RollbackValidationState ValidationState,
    string Message);
