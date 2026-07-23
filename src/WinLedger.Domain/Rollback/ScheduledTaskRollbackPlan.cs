namespace WinLedger.Domain.Rollback;

public sealed record ScheduledTaskRollbackPlan(
    Guid Id,
    Guid ComparisonId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ScheduledTaskRollbackOperation> Operations,
    IReadOnlyList<string> Warnings);
