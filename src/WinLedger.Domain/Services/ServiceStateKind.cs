namespace WinLedger.Domain.Services;

public enum ServiceStateKind
{
    Unknown,
    Stopped,
    StartPending,
    StopPending,
    Running,
    ContinuePending,
    PausePending,
    Paused
}
