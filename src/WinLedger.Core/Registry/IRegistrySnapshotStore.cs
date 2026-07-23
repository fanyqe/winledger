using WinLedger.Domain.Registry;
using WinLedger.Core.Sessions;

namespace WinLedger.Core.Registry;

public interface IRegistrySnapshotStore : ITrackingSessionStore
{
    Task SaveRegistrySnapshotAsync(RegistrySnapshot snapshot, CancellationToken cancellationToken);

    Task<RegistrySnapshot?> GetRegistrySnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RegistrySnapshot>> ListRegistrySnapshotsAsync(Guid sessionId, CancellationToken cancellationToken);
}
