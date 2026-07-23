namespace WinLedger.Domain.Rollback;

public sealed record FileSystemRollbackPlan(
    Guid Id,
    Guid ComparisonId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<FileSystemRollbackOperation> Operations,
    IReadOnlyList<string> Warnings);
