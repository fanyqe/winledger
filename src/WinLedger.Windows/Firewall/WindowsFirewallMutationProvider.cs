using WinLedger.Core.Firewall;
using WinLedger.Domain.Firewall;

namespace WinLedger.Windows.Firewall;

public sealed class WindowsFirewallMutationProvider : IFirewallMutationProvider
{
    public Task<IReadOnlyList<WindowsFirewallRuleSnapshot>> ReadRulesByNameAsync(
        string ruleName,
        CancellationToken cancellationToken)
    {
        var rules = FindRules(ruleName, cancellationToken);
        return Task.FromResult(WindowsFirewallRuleMapper.AnnotateDuplicateNames(rules));
    }

    public Task DeleteRuleAsync(
        string ruleName,
        CancellationToken cancellationToken)
    {
        dynamic policy = WindowsFirewallSnapshotCollector.CreatePolicy();
        var matches = FindComRules(policy, ruleName, cancellationToken);
        if (matches.Count != 1)
        {
            throw new InvalidOperationException("Firewall rule deletion requires exactly one matching rule name.");
        }

        policy.Rules.Remove(ruleName);
        return Task.CompletedTask;
    }

    public Task SetRuleEnabledAsync(
        string ruleName,
        bool enabled,
        CancellationToken cancellationToken)
    {
        dynamic policy = WindowsFirewallSnapshotCollector.CreatePolicy();
        var matches = FindComRules(policy, ruleName, cancellationToken);
        if (matches.Count != 1)
        {
            throw new InvalidOperationException("Firewall rule mutation requires exactly one matching rule name.");
        }

        matches[0].Enabled = enabled;
        return Task.CompletedTask;
    }

    private static IReadOnlyList<WindowsFirewallRuleSnapshot> FindRules(
        string ruleName,
        CancellationToken cancellationToken)
    {
        var snapshots = new List<WindowsFirewallRuleSnapshot>();
        var occurrenceByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        dynamic policy = WindowsFirewallSnapshotCollector.CreatePolicy();

        foreach (dynamic rule in FindComRules(policy, ruleName, cancellationToken))
        {
            string name = rule.Name;
            occurrenceByName.TryGetValue(name, out var occurrence);
            occurrenceByName[name] = occurrence + 1;
            snapshots.Add(WindowsFirewallRuleMapper.FromComRule(
                rule,
                WindowsFirewallRuleMapper.IdentityFor(rule, occurrence),
                false));
        }

        return snapshots;
    }

    private static IReadOnlyList<dynamic> FindComRules(
        dynamic policy,
        string ruleName,
        CancellationToken cancellationToken)
    {
        var matches = new List<dynamic>();

        foreach (dynamic rule in policy.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = rule.Name;
            if (string.Equals(name, ruleName, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(rule);
            }
        }

        return matches;
    }
}
