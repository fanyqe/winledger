namespace WinLedger.Domain.Rollback;

public sealed record EnvironmentRollbackPlan(
    Guid Id,
    Guid ComparisonId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<EnvironmentRollbackOperation> Operations,
    IReadOnlyList<string> Warnings);
