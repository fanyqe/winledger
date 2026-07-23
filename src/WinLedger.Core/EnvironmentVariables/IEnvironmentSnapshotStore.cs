using WinLedger.Core.Sessions;
using WinLedger.Domain.EnvironmentVariables;

namespace WinLedger.Core.EnvironmentVariables;

public interface IEnvironmentSnapshotStore : ITrackingSessionStore
{
    Task SaveEnvironmentSnapshotAsync(EnvironmentSnapshot snapshot, CancellationToken cancellationToken);

    Task<EnvironmentSnapshot?> GetEnvironmentSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);

    Task<IReadOnlyList<EnvironmentSnapshot>> ListEnvironmentSnapshotsAsync(Guid sessionId, CancellationToken cancellationToken);
}
