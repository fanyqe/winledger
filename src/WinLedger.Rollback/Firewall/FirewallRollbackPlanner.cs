using WinLedger.Domain.Firewall;
using WinLedger.Domain.Rollback;

namespace WinLedger.Rollback.Firewall;

public sealed class FirewallRollbackPlanner
{
    public FirewallRollbackPlan CreatePlan(FirewallComparison comparison, DateTimeOffset createdAt)
    {
        var operations = new List<FirewallRollbackOperation>();
        var warnings = new List<string>();
        var plannedRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var change in comparison.Changes)
        {
            if (!plannedRules.Add(change.RuleName))
            {
                continue;
            }

            if (change.Before?.HasDuplicateName == true || change.After?.HasDuplicateName == true)
            {
                warnings.Add($"Firewall rollback requires manual review for duplicate rule name: {change.TargetDisplayName}");
                continue;
            }

            switch (change.Kind)
            {
                case FirewallRuleChangeKind.RuleCreated:
                    if (change.After is null)
                    {
                        warnings.Add($"Firewall rollback requires manual review: {change.TargetDisplayName}");
                        break;
                    }

                    operations.Add(new FirewallRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        FirewallRollbackOperationKind.DeleteFirewallRule,
                        change.RuleName,
                        change.After,
                        null,
                        true,
                        false));
                    break;

                case FirewallRuleChangeKind.EnabledChanged:
                    if (change.Before is null || change.After is null)
                    {
                        warnings.Add($"Firewall rollback requires manual review: {change.TargetDisplayName}");
                        break;
                    }

                    operations.Add(new FirewallRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        FirewallRollbackOperationKind.SetFirewallRuleEnabled,
                        change.RuleName,
                        change.After,
                        change.Before.Enabled,
                        true,
                        false));
                    break;

                default:
                    warnings.Add($"Unsupported firewall rollback change kind: {change.Kind} at {change.TargetDisplayName}");
                    break;
            }
        }

        return new FirewallRollbackPlan(Guid.NewGuid(), comparison.Id, createdAt, operations, warnings);
    }
}
