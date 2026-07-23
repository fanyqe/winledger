namespace WinLedger.Domain.ScheduledTasks;

public sealed record ScheduledTaskTriggerSnapshot(
    ScheduledTaskTriggerKind Kind,
    bool Enabled,
    string? StartBoundary,
    string? EndBoundary,
    string Details);
