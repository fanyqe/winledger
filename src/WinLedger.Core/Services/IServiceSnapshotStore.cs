using WinLedger.Core.Sessions;
using WinLedger.Domain.Services;

namespace WinLedger.Core.Services;

public interface IServiceSnapshotStore : ITrackingSessionStore
{
    Task SaveServiceSnapshotAsync(ServiceSnapshot snapshot, CancellationToken cancellationToken);

    Task<ServiceSnapshot?> GetServiceSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceSnapshot>> ListServiceSnapshotsAsync(Guid sessionId, CancellationToken cancellationToken);
}
