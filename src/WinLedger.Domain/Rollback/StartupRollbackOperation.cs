using WinLedger.Domain.Startup;

namespace WinLedger.Domain.Rollback;

public sealed record StartupRollbackOperation(
    Guid Id,
    Guid ChangeId,
    StartupRollbackOperationKind Kind,
    StartupEntrySnapshot ExpectedCurrentEntry,
    bool RequiresAdministrator,
    bool RequiresRestart)
{
    public string TargetDisplayName => ExpectedCurrentEntry.Location;
}
