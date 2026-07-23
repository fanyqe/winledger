namespace WinLedger.Domain.Rollback;

public sealed record StartupRollbackPlan(
    Guid Id,
    Guid ComparisonId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<StartupRollbackOperation> Operations,
    IReadOnlyList<string> Warnings);
