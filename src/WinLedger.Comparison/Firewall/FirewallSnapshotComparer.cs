using WinLedger.Domain.Firewall;

namespace WinLedger.Comparison.Firewall;

public sealed class FirewallSnapshotComparer
{
    public FirewallComparison Compare(FirewallSnapshot baseline, FirewallSnapshot comparison, DateTimeOffset comparedAt)
    {
        if (baseline.SessionId != comparison.SessionId)
        {
            throw new ArgumentException("Firewall snapshots belong to different sessions.", nameof(comparison));
        }

        var changes = new List<FirewallRuleChange>();
        var baselineRules = baseline.Rules.ToDictionary(rule => rule.Identity, StringComparer.OrdinalIgnoreCase);
        var comparisonRules = comparison.Rules.ToDictionary(rule => rule.Identity, StringComparer.OrdinalIgnoreCase);

        foreach (var (identity, after) in comparisonRules)
        {
            if (!baselineRules.TryGetValue(identity, out var before))
            {
                changes.Add(CreateChange(FirewallRuleChangeKind.RuleCreated, null, after));
                continue;
            }

            AddRulePropertyChanges(before, after, changes);
        }

        foreach (var (identity, before) in baselineRules)
        {
            if (!comparisonRules.ContainsKey(identity))
            {
                changes.Add(CreateChange(FirewallRuleChangeKind.RuleRemoved, before, null));
            }
        }

        return new FirewallComparison(
            Guid.NewGuid(),
            baseline.SessionId,
            baseline.Id,
            comparison.Id,
            comparedAt,
            changes.OrderBy(change => change.TargetDisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(change => change.Kind).ToArray(),
            baseline.Warnings.Concat(comparison.Warnings).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void AddRulePropertyChanges(
        WindowsFirewallRuleSnapshot before,
        WindowsFirewallRuleSnapshot after,
        List<FirewallRuleChange> changes)
    {
        if (before.Enabled != after.Enabled)
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.EnabledChanged, before, after));
        }

        if (before.Action != after.Action)
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.ActionChanged, before, after));
        }

        if (before.Direction != after.Direction)
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.DirectionChanged, before, after));
        }

        if (!string.Equals(before.ApplicationPath, after.ApplicationPath, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.ApplicationPathChanged, before, after));
        }

        if (!string.Equals(before.ServiceName, after.ServiceName, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.ServiceNameChanged, before, after));
        }

        if (before.Protocol != after.Protocol || before.ProtocolNumber != after.ProtocolNumber)
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.ProtocolChanged, before, after));
        }

        if (!string.Equals(before.LocalPorts, after.LocalPorts, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(before.RemotePorts, after.RemotePorts, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(before.IcmpTypesAndCodes, after.IcmpTypesAndCodes, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.PortsChanged, before, after));
        }

        if (before.Profiles != after.Profiles ||
            !before.ProfileNames.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(after.ProfileNames.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.ProfilesChanged, before, after));
        }

        if (!string.Equals(before.LocalAddresses, after.LocalAddresses, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(before.RemoteAddresses, after.RemoteAddresses, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.AddressesChanged, before, after));
        }

        if (!string.Equals(before.InterfaceTypes, after.InterfaceTypes, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.InterfaceTypesChanged, before, after));
        }

        if (before.EdgeTraversal != after.EdgeTraversal)
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.EdgeTraversalChanged, before, after));
        }

        if (!string.Equals(before.Description, after.Description, StringComparison.Ordinal))
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.DescriptionChanged, before, after));
        }

        if (!string.Equals(before.Grouping, after.Grouping, StringComparison.Ordinal))
        {
            changes.Add(CreateChange(FirewallRuleChangeKind.GroupingChanged, before, after));
        }
    }

    private static FirewallRuleChange CreateChange(
        FirewallRuleChangeKind kind,
        WindowsFirewallRuleSnapshot? before,
        WindowsFirewallRuleSnapshot? after)
    {
        var availability = FirewallRuleChangeExplainer.GetRollbackAvailability(kind, before, after);
        var ruleName = after?.Name ?? before?.Name ?? "(unknown)";

        return new FirewallRuleChange(
            Guid.NewGuid(),
            kind,
            ruleName,
            before,
            after,
            FirewallRuleChangeExplainer.Summarize(kind, before, after),
            FirewallRuleChangeExplainer.Classify(kind, before, after, availability),
            availability);
    }
}
