using WinLedger.Domain.Rollback;

namespace WinLedger.Domain.Hosts;

public sealed record HostsFileChange(
    Guid Id,
    HostsFileChangeKind Kind,
    string FilePath,
    HostsFileLineSnapshot? BeforeLine,
    HostsFileLineSnapshot? AfterLine,
    HostsFileSnapshot? Before,
    HostsFileSnapshot? After,
    string Summary,
    IReadOnlySet<ChangeAttentionLabel> Labels,
    RollbackAvailability RollbackAvailability)
{
    public string TargetDisplayName => Kind switch
    {
        HostsFileChangeKind.LineAdded => $"{FilePath}: line {AfterLine?.LineNumber}",
        HostsFileChangeKind.LineRemoved => $"{FilePath}: line {BeforeLine?.LineNumber}",
        _ => FilePath
    };
}
