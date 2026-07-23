namespace WinLedger.Domain.EnvironmentVariables;

public sealed record EnvironmentComparison(
    Guid Id,
    Guid SessionId,
    Guid BaselineSnapshotId,
    Guid ComparisonSnapshotId,
    DateTimeOffset ComparedAt,
    IReadOnlyList<EnvironmentVariableChange> Changes,
    IReadOnlyList<string> Warnings);
