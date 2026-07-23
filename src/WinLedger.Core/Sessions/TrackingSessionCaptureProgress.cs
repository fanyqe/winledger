namespace WinLedger.Core.Sessions;

public sealed record TrackingSessionCaptureProgress(
    TrackingSnapshotStage Stage,
    TrackingSubsystemKind Subsystem,
    int CompletedSubsystems,
    int TotalSubsystems,
    string Message);
