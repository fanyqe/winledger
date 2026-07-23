using WinLedger.Domain.Sessions;

namespace WinLedger.Core.Sessions;

public sealed record TrackingSessionCaptureResult(
    TrackingSession Session,
    TrackingSnapshotStage Stage,
    IReadOnlyList<TrackingSnapshotCaptureSummary> Snapshots);
