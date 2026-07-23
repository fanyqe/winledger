namespace WinLedger.Domain.InstalledApplications;

public sealed record InstalledApplicationsComparison(
    Guid Id,
    Guid SessionId,
    Guid BaselineSnapshotId,
    Guid ComparisonSnapshotId,
    DateTimeOffset ComparedAt,
    IReadOnlyList<InstalledApplicationChange> Changes,
    IReadOnlyList<string> Warnings);
