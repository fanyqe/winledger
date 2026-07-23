using WinLedger.Core.Sessions;
using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Core.ScheduledTasks;

public interface IScheduledTaskSnapshotStore : ITrackingSessionStore
{
    Task SaveScheduledTaskSnapshotAsync(ScheduledTaskSnapshot snapshot, CancellationToken cancellationToken);

    Task<ScheduledTaskSnapshot?> GetScheduledTaskSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduledTaskSnapshot>> ListScheduledTaskSnapshotsAsync(Guid sessionId, CancellationToken cancellationToken);
}
