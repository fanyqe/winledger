namespace WinLedger.Domain.Services;

public sealed record ServiceComparison(
    Guid Id,
    Guid SessionId,
    Guid BaselineSnapshotId,
    Guid ComparisonSnapshotId,
    DateTimeOffset ComparedAt,
    IReadOnlyList<ServiceChange> Changes,
    IReadOnlyList<string> Warnings);
