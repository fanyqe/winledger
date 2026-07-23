namespace WinLedger.Domain.Rollback;

public sealed record HostsFileRollbackResult(
    Guid OperationId,
    bool Succeeded,
    RollbackValidationState ValidationState,
    string Message);
