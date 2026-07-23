using WinLedger.Core.Sessions;
using WinLedger.Domain.InstalledApplications;

namespace WinLedger.Core.InstalledApplications;

public interface IInstalledApplicationSnapshotStore : ITrackingSessionStore
{
    Task SaveInstalledApplicationsSnapshotAsync(
        InstalledApplicationsSnapshot snapshot,
        CancellationToken cancellationToken);

    Task<InstalledApplicationsSnapshot?> GetInstalledApplicationsSnapshotAsync(
        Guid snapshotId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InstalledApplicationsSnapshot>> ListInstalledApplicationsSnapshotsAsync(
        Guid sessionId,
        CancellationToken cancellationToken);
}
