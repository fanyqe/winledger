namespace WinLedger.Domain.Hosts;

public sealed record HostsFileComparison(
    Guid Id,
    Guid SessionId,
    Guid BaselineSnapshotId,
    Guid ComparisonSnapshotId,
    DateTimeOffset ComparedAt,
    IReadOnlyList<HostsFileChange> Changes,
    IReadOnlyList<string> Warnings);
