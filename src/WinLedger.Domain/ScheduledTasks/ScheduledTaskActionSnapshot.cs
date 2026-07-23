namespace WinLedger.Domain.ScheduledTasks;

public sealed record ScheduledTaskActionSnapshot(
    ScheduledTaskActionKind Kind,
    string? ExecutablePath,
    string? Arguments,
    string? WorkingDirectory,
    string Details);
