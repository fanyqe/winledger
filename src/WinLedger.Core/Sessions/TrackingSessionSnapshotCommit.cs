namespace WinLedger.Core.Sessions;

public sealed record TrackingSessionSnapshotCommit(
    TrackingSubsystemKind Subsystem,
    object Snapshot);
