namespace WinLedger.Domain.Rollback;

public sealed record ServiceRollbackPlan(
    Guid Id,
    Guid ComparisonId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ServiceRollbackOperation> Operations,
    IReadOnlyList<string> Warnings);
