using WinLedger.Core.Sessions;
using WinLedger.Domain.Hosts;

namespace WinLedger.Core.Hosts;

public interface IHostsFileSnapshotStore : ITrackingSessionStore
{
    Task SaveHostsFileSnapshotAsync(HostsFileSnapshot snapshot, CancellationToken cancellationToken);

    Task<HostsFileSnapshot?> GetHostsFileSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);

    Task<IReadOnlyList<HostsFileSnapshot>> ListHostsFileSnapshotsAsync(Guid sessionId, CancellationToken cancellationToken);
}
