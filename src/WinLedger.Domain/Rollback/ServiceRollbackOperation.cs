using WinLedger.Domain.Services;

namespace WinLedger.Domain.Rollback;

public sealed record ServiceRollbackOperation(
    Guid Id,
    Guid ChangeId,
    ServiceRollbackOperationKind Kind,
    string ServiceName,
    WindowsServiceSnapshot ExpectedCurrentState,
    ServiceStartModeKind? RestoreStartMode,
    bool? RestoreDelayedAutoStart,
    bool RequiresAdministrator,
    bool RequiresRestart)
{
    public string TargetDisplayName => ServiceName;
}
