namespace WinLedger.Domain.ScheduledTasks;

public enum ScheduledTaskTriggerKind
{
    Unknown,
    Once,
    Daily,
    Weekly,
    Monthly,
    MonthlyDayOfWeek,
    Idle,
    Registration,
    Boot,
    Logon,
    SessionStateChange,
    Event
}
