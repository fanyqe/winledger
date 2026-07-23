namespace WinLedger.Domain.ScheduledTasks;

public sealed record ScheduledTaskComparison(
    Guid Id,
    Guid SessionId,
    Guid BaselineSnapshotId,
    Guid ComparisonSnapshotId,
    DateTimeOffset ComparedAt,
    IReadOnlyList<ScheduledTaskChange> Changes,
    IReadOnlyList<string> Warnings);
