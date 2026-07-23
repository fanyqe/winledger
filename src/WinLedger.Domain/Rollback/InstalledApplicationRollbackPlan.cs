namespace WinLedger.Domain.Rollback;

public sealed record InstalledApplicationRollbackPlan(
    Guid Id,
    Guid ComparisonId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<InstalledApplicationRollbackOperation> Operations,
    IReadOnlyList<string> Warnings);
