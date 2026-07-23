using WinLedger.Domain.Rollback;

namespace WinLedger.Domain.Firewall;

public sealed record FirewallRuleChange(
    Guid Id,
    FirewallRuleChangeKind Kind,
    string RuleName,
    WindowsFirewallRuleSnapshot? Before,
    WindowsFirewallRuleSnapshot? After,
    string Summary,
    IReadOnlySet<ChangeAttentionLabel> Labels,
    RollbackAvailability RollbackAvailability)
{
    public string TargetDisplayName => RuleName;
}
