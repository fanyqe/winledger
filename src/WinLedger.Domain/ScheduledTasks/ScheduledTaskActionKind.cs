namespace WinLedger.Domain.ScheduledTasks;

public enum ScheduledTaskActionKind
{
    Unknown,
    Execute,
    ComHandler,
    SendEmail,
    ShowMessage
}
