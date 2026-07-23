namespace WinLedger.Domain.Rollback;

public sealed record FirewallRollbackResult(
    Guid OperationId,
    bool Succeeded,
    RollbackValidationState ValidationState,
    string Message);
