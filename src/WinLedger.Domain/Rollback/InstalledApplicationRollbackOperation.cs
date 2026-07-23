using WinLedger.Domain.InstalledApplications;

namespace WinLedger.Domain.Rollback;

public sealed record InstalledApplicationRollbackOperation(
    Guid Id,
    Guid ChangeId,
    InstalledApplicationRollbackOperationKind Kind,
    string ApplicationName,
    InstalledApplicationSnapshot? ExpectedCurrentApplication,
    bool RequiresAdministrator,
    bool RequiresRestart,
    string Instructions)
{
    public string TargetDisplayName => ApplicationName;
}
