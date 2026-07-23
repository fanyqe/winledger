using WinLedger.Domain.Sessions;

namespace WinLedger.Core.Sessions;

public interface ITrackingSessionCaptureCommitStore
{
    Task CommitCaptureAsync(
        TrackingSession session,
        IReadOnlyList<TrackingSessionSnapshotCommit> snapshots,
        CancellationToken cancellationToken);
}
