namespace WinLedger.Domain.Sessions;

public enum TrackingSessionStatus
{
    Created,
    BaselineCaptured,
    Compared,
    RollbackPlanned,
    RollbackApplied,
    Failed
}
