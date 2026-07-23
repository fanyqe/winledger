using WinLedger.Domain.Firewall;

namespace WinLedger.Domain.Rollback;

public sealed record FirewallRollbackOperation(
    Guid Id,
    Guid ChangeId,
    FirewallRollbackOperationKind Kind,
    string RuleName,
    WindowsFirewallRuleSnapshot ExpectedCurrentRule,
    bool? RestoreEnabled,
    bool RequiresAdministrator,
    bool RequiresRestart)
{
    public string TargetDisplayName => RuleName;
}
