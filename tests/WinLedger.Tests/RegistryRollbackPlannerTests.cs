using WinLedger.Comparison.Registry;
using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.Registry;

namespace WinLedger.Tests;

public sealed class RegistryRollbackPlannerTests
{
    [Fact]
    public void CreatePlanCreatesValueRollbackOperationsOnly()
    {
        var sessionId = Guid.NewGuid();
        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox");
        var baseline = RegistrySnapshot.Empty(sessionId, "Baseline", DateTimeOffset.UtcNow) with
        {
            Keys =
            [
                new RegistryKeySnapshot(
                    keyPath,
                    [new RegistryValueSnapshot("Setting", RegistryValueType.String, "\"before\"", "before")])
            ]
        };
        var comparisonSnapshot = baseline with
        {
            Id = Guid.NewGuid(),
            Keys =
            [
                new RegistryKeySnapshot(
                    keyPath,
                    [new RegistryValueSnapshot("Setting", RegistryValueType.String, "\"after\"", "after")])
            ]
        };
        var comparison = new RegistrySnapshotComparer().Compare(baseline, comparisonSnapshot, DateTimeOffset.UtcNow);

        var plan = new RegistryRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);
        var operation = Assert.Single(plan.Operations);

        Assert.Equal(RollbackOperationKind.SetRegistryValue, operation.Kind);
        Assert.Equal("Setting", operation.ValueName);
        Assert.False(operation.RequiresAdministrator);
        Assert.Equal("\"before\"", operation.RestoreValue?.SerializedValue);
    }
}
