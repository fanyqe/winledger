namespace WinLedger.Domain.Rollback;

public sealed record FirewallRollbackPlan(
    Guid Id,
    Guid ComparisonId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<FirewallRollbackOperation> Operations,
    IReadOnlyList<string> Warnings);
