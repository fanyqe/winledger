namespace WinLedger.Domain.Firewall;

public sealed record FirewallComparison(
    Guid Id,
    Guid SessionId,
    Guid BaselineSnapshotId,
    Guid ComparisonSnapshotId,
    DateTimeOffset ComparedAt,
    IReadOnlyList<FirewallRuleChange> Changes,
    IReadOnlyList<string> Warnings);
