namespace WinLedger.Domain.Sessions;

public enum TrackingSessionStatus
{
    Created,
    BaselineCaptured,
    ComparisonCaptured,
    Compared,
    RollbackPlanned,
    RollbackApplied,
    Failed
}
