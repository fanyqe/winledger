namespace WinLedger.Domain.Rollback;

public sealed record HostsFileRollbackPlan(
    Guid Id,
    Guid ComparisonId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<HostsFileRollbackOperation> Operations,
    IReadOnlyList<string> Warnings);
