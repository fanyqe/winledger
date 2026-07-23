namespace WinLedger.Domain.Hosts;

public enum HostsFileChangeKind
{
    FileCreated,
    FileRemoved,
    ContentChanged,
    LineAdded,
    LineRemoved
}
