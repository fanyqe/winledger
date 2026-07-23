using WinLedger.Comparison.Services;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.Services;
using WinLedger.Rollback.Services;

namespace WinLedger.Tests;

public sealed class ServiceRollbackPlannerTests
{
    [Fact]
    public void CreatePlanAddsOperationsForSupportedServiceConfigurationChanges()
    {
        var sessionId = Guid.NewGuid();
        var before = Service(ServiceStartModeKind.Automatic, false);
        var after = Service(ServiceStartModeKind.Disabled, true);
        var comparison = new ServiceSnapshotComparer().Compare(
            new ServiceSnapshot(Guid.NewGuid(), sessionId, "Before", DateTimeOffset.UtcNow, [before], []),
            new ServiceSnapshot(Guid.NewGuid(), sessionId, "After", DateTimeOffset.UtcNow, [after], []),
            DateTimeOffset.UtcNow);

        var plan = new ServiceRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.Contains(plan.Operations, operation =>
            operation.Kind == ServiceRollbackOperationKind.SetServiceStartMode &&
            operation.RestoreStartMode == ServiceStartModeKind.Automatic &&
            operation.RequiresAdministrator &&
            operation.RequiresRestart);
        Assert.Contains(plan.Operations, operation =>
            operation.Kind == ServiceRollbackOperationKind.SetServiceDelayedAutoStart &&
            operation.RestoreDelayedAutoStart == false &&
            operation.RequiresAdministrator &&
            operation.RequiresRestart);
    }

    [Fact]
    public void CreatePlanSkipsUnknownStartModes()
    {
        var sessionId = Guid.NewGuid();
        var before = Service(ServiceStartModeKind.Unknown, false);
        var after = Service(ServiceStartModeKind.Disabled, false);
        var comparison = new ServiceSnapshotComparer().Compare(
            new ServiceSnapshot(Guid.NewGuid(), sessionId, "Before", DateTimeOffset.UtcNow, [before], []),
            new ServiceSnapshot(Guid.NewGuid(), sessionId, "After", DateTimeOffset.UtcNow, [after], []),
            DateTimeOffset.UtcNow);

        var plan = new ServiceRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.Empty(plan.Operations);
        Assert.Contains(plan.Warnings, warning => warning.Contains("manual review", StringComparison.OrdinalIgnoreCase));
    }

    private static WindowsServiceSnapshot Service(ServiceStartModeKind startMode, bool delayedAutoStart)
    {
        return new WindowsServiceSnapshot(
            "ExampleService",
            "Example Service",
            startMode,
            @"C:\Example\service.exe",
            "LocalSystem",
            ServiceStateKind.Running,
            delayedAutoStart,
            [],
            null);
    }
}
