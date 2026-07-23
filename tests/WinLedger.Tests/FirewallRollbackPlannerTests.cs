using WinLedger.Comparison.Firewall;
using WinLedger.Domain.Firewall;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.Firewall;

namespace WinLedger.Tests;

public sealed class FirewallRollbackPlannerTests
{
    [Fact]
    public void CreatePlanDeletesCreatedRuleAndRestoresEnabledState()
    {
        var sessionId = Guid.NewGuid();
        var before = FirewallTestData.Rule("Existing rule", enabled: false);
        var after = before with { Enabled = true };
        var created = FirewallTestData.Rule("Created rule");
        var comparison = new FirewallSnapshotComparer().Compare(
            FirewallTestData.Snapshot(sessionId, before),
            FirewallTestData.Snapshot(sessionId, after, created),
            DateTimeOffset.UtcNow);

        var plan = new FirewallRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.Contains(plan.Operations, operation =>
            operation.Kind == FirewallRollbackOperationKind.DeleteFirewallRule &&
            operation.RuleName == "Created rule" &&
            operation.RestoreEnabled is null &&
            operation.RequiresAdministrator &&
            !operation.RequiresRestart);
        Assert.Contains(plan.Operations, operation =>
            operation.Kind == FirewallRollbackOperationKind.SetFirewallRuleEnabled &&
            operation.RuleName == "Existing rule" &&
            operation.RestoreEnabled == false &&
            operation.RequiresAdministrator);
    }

    [Fact]
    public void CreatePlanWarnsForUnsupportedAndDuplicateRuleChanges()
    {
        var sessionId = Guid.NewGuid();
        var before = FirewallTestData.Rule("Changed rule");
        var after = before with { LocalPorts = "8080" };
        var duplicate = FirewallTestData.Rule("Duplicate rule", duplicateName: true);
        var comparison = new FirewallSnapshotComparer().Compare(
            FirewallTestData.Snapshot(sessionId, before),
            FirewallTestData.Snapshot(sessionId, after, duplicate),
            DateTimeOffset.UtcNow);

        var plan = new FirewallRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.DoesNotContain(plan.Operations, operation => operation.RuleName == "Changed rule");
        Assert.DoesNotContain(plan.Operations, operation => operation.RuleName == "Duplicate rule");
        Assert.Contains(plan.Warnings, warning => warning.Contains("Unsupported firewall rollback", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Warnings, warning => warning.Contains("duplicate rule name", StringComparison.OrdinalIgnoreCase));
    }
}
