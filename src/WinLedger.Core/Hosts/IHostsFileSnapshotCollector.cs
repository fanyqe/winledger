using WinLedger.Domain.Hosts;

namespace WinLedger.Core.Hosts;

public interface IHostsFileSnapshotCollector
{
    Task<HostsFileSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken);
}
