using WinLedger.Domain.Rollback;

namespace WinLedger.Core.Elevation;

public sealed record ElevatedRollbackOperationResult(
    Guid OperationId,
    bool Succeeded,
    RollbackValidationState ValidationState,
    string Message);
