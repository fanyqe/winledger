namespace WinLedger.Domain.Rollback;

public sealed record RegistryRollbackPlan(
    Guid Id,
    Guid ComparisonId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RegistryRollbackOperation> Operations,
    IReadOnlyList<string> Warnings);
