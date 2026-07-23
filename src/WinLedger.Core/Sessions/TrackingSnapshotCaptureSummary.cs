namespace WinLedger.Core.Sessions;

public sealed record TrackingSnapshotCaptureSummary(
    TrackingSubsystemKind Subsystem,
    Guid SnapshotId,
    string SnapshotName,
    DateTimeOffset CapturedAt,
    int ItemCount,
    int WarningCount);
