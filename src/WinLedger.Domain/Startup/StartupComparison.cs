namespace WinLedger.Domain.Startup;

public sealed record StartupComparison(
    Guid Id,
    Guid SessionId,
    Guid BaselineSnapshotId,
    Guid ComparisonSnapshotId,
    DateTimeOffset ComparedAt,
    IReadOnlyList<StartupChange> Changes,
    IReadOnlyList<string> Warnings);
