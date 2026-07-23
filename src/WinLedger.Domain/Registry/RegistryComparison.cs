namespace WinLedger.Domain.Registry;

public sealed record RegistryComparison(
    Guid Id,
    Guid SessionId,
    Guid BaselineSnapshotId,
    Guid ComparisonSnapshotId,
    DateTimeOffset ComparedAt,
    IReadOnlyList<RegistryChange> Changes,
    IReadOnlyList<string> Warnings);
