using WinLedger.Core.Sessions;
using WinLedger.Domain.Startup;

namespace WinLedger.Core.Startup;

public interface IStartupSnapshotStore : ITrackingSessionStore
{
    Task SaveStartupSnapshotAsync(StartupSnapshot snapshot, CancellationToken cancellationToken);

    Task<StartupSnapshot?> GetStartupSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StartupSnapshot>> ListStartupSnapshotsAsync(Guid sessionId, CancellationToken cancellationToken);
}
