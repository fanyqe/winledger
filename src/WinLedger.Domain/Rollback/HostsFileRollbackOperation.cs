using WinLedger.Domain.Hosts;

namespace WinLedger.Domain.Rollback;

public sealed record HostsFileRollbackOperation(
    Guid Id,
    Guid ChangeId,
    HostsFileRollbackOperationKind Kind,
    string FilePath,
    HostsFileSnapshot ExpectedCurrentSnapshot,
    string? RestoreContentBase64,
    bool RequiresAdministrator,
    bool RequiresRestart)
{
    public string TargetDisplayName => FilePath;
}
