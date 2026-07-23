using WinLedger.Domain.Firewall;
using WinLedger.Domain.Rollback;

namespace WinLedger.Comparison.Firewall;

public static class FirewallRuleChangeExplainer
{
    public static string Summarize(
        FirewallRuleChangeKind kind,
        WindowsFirewallRuleSnapshot? before,
        WindowsFirewallRuleSnapshot? after)
    {
        var rule = after ?? before;
        var name = rule?.Name ?? "unknown rule";

        return kind switch
        {
            FirewallRuleChangeKind.RuleCreated => $"A firewall rule named \"{name}\" was created.",
            FirewallRuleChangeKind.RuleRemoved => $"The firewall rule \"{name}\" was removed.",
            FirewallRuleChangeKind.EnabledChanged => $"The firewall rule \"{name}\" was {(after?.Enabled == true ? "enabled" : "disabled")}.",
            FirewallRuleChangeKind.ActionChanged => $"The firewall rule \"{name}\" action changed from {Display(before?.Action)} to {Display(after?.Action)}.",
            FirewallRuleChangeKind.DirectionChanged => $"The firewall rule \"{name}\" direction changed from {Display(before?.Direction)} to {Display(after?.Direction)}.",
            FirewallRuleChangeKind.ApplicationPathChanged => $"The firewall rule \"{name}\" application path changed from {Display(before?.ApplicationPath)} to {Display(after?.ApplicationPath)}.",
            FirewallRuleChangeKind.ServiceNameChanged => $"The firewall rule \"{name}\" service changed from {Display(before?.ServiceName)} to {Display(after?.ServiceName)}.",
            FirewallRuleChangeKind.ProtocolChanged => $"The firewall rule \"{name}\" protocol changed from {DisplayProtocol(before)} to {DisplayProtocol(after)}.",
            FirewallRuleChangeKind.PortsChanged => $"The firewall rule \"{name}\" port configuration changed.",
            FirewallRuleChangeKind.ProfilesChanged => $"The firewall rule \"{name}\" network profiles changed from {DisplayProfiles(before)} to {DisplayProfiles(after)}.",
            FirewallRuleChangeKind.AddressesChanged => $"The firewall rule \"{name}\" address scope changed.",
            FirewallRuleChangeKind.InterfaceTypesChanged => $"The firewall rule \"{name}\" interface types changed.",
            FirewallRuleChangeKind.EdgeTraversalChanged => $"The firewall rule \"{name}\" edge traversal setting changed.",
            FirewallRuleChangeKind.DescriptionChanged => $"The firewall rule \"{name}\" description changed.",
            FirewallRuleChangeKind.GroupingChanged => $"The firewall rule \"{name}\" group changed.",
            _ => $"The firewall rule \"{name}\" changed."
        };
    }

    public static IReadOnlySet<ChangeAttentionLabel> Classify(
        FirewallRuleChangeKind kind,
        WindowsFirewallRuleSnapshot? before,
        WindowsFirewallRuleSnapshot? after,
        RollbackAvailability rollbackAvailability)
    {
        var labels = new HashSet<ChangeAttentionLabel>
        {
            ChangeAttentionLabel.Persistent,
            ChangeAttentionLabel.NetworkRelated,
            ChangeAttentionLabel.SecuritySensitive,
            ChangeAttentionLabel.Privileged
        };

        var rule = after ?? before;
        if (kind is FirewallRuleChangeKind.RuleRemoved or FirewallRuleChangeKind.ActionChanged or
            FirewallRuleChangeKind.DirectionChanged or FirewallRuleChangeKind.PortsChanged or
            FirewallRuleChangeKind.ProfilesChanged or FirewallRuleChangeKind.AddressesChanged ||
            rule?.Action is FirewallRuleActionKind.Allow)
        {
            labels.Add(ChangeAttentionLabel.PotentiallyDestructive);
        }

        if (rollbackAvailability is RollbackAvailability.Unavailable or RollbackAvailability.ManualReview)
        {
            labels.Add(ChangeAttentionLabel.RollbackUnavailable);
        }

        return labels;
    }

    public static RollbackAvailability GetRollbackAvailability(
        FirewallRuleChangeKind kind,
        WindowsFirewallRuleSnapshot? before,
        WindowsFirewallRuleSnapshot? after)
    {
        if (before?.HasDuplicateName == true || after?.HasDuplicateName == true)
        {
            return RollbackAvailability.ManualReview;
        }

        return kind switch
        {
            FirewallRuleChangeKind.RuleCreated when after is not null => RollbackAvailability.RequiresConfirmation,
            FirewallRuleChangeKind.EnabledChanged when before is not null && after is not null => RollbackAvailability.RequiresConfirmation,
            _ => RollbackAvailability.ManualReview
        };
    }

    private static string Display<T>(T? value)
    {
        return value is null ? "(empty)" : $"\"{value}\"";
    }

    private static string DisplayProtocol(WindowsFirewallRuleSnapshot? rule)
    {
        return rule is null
            ? "(empty)"
            : $"{rule.Protocol} ({rule.ProtocolNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
    }

    private static string DisplayProfiles(WindowsFirewallRuleSnapshot? rule)
    {
        return rule is null || rule.ProfileNames.Count == 0
            ? "(none)"
            : string.Join(", ", rule.ProfileNames);
    }
}
