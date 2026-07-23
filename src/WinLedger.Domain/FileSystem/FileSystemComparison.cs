namespace WinLedger.Domain.FileSystem;

public sealed record FileSystemComparison(
    Guid Id,
    Guid SessionId,
    Guid BaselineSnapshotId,
    Guid ComparisonSnapshotId,
    DateTimeOffset ComparedAt,
    IReadOnlyList<FileSystemChange> Changes,
    IReadOnlyList<string> Warnings);
