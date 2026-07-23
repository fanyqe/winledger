using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Core.ScheduledTasks;

public interface IScheduledTaskSnapshotCollector
{
    Task<ScheduledTaskSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken);
}
