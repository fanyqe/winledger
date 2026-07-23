namespace WinLedger.Domain.ScheduledTasks;

public enum ScheduledTaskStateKind
{
    Unknown,
    Disabled,
    Queued,
    Ready,
    Running
}
