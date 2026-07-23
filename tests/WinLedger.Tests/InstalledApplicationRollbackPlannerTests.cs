using WinLedger.Comparison.InstalledApplications;
using WinLedger.Rollback.InstalledApplications;

namespace WinLedger.Tests;

public sealed class InstalledApplicationRollbackPlannerTests
{
    [Fact]
    public void CreatePlanCreatesNoAutomaticOperationsAndWarnsForEachChange()
    {
        var sessionId = Guid.NewGuid();
        var before = InstalledApplicationTestData.Application("Example App", "ExampleApp", displayVersion: "1.0.0");
        var after = before with
        {
            DisplayVersion = "2.0.0",
            UninstallString = @"""C:\Example\remove.exe"""
        };
        var comparison = new InstalledApplicationsSnapshotComparer().Compare(
            InstalledApplicationTestData.Snapshot(sessionId, before),
            InstalledApplicationTestData.Snapshot(sessionId, after),
            DateTimeOffset.UtcNow);

        var plan = new InstalledApplicationRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.Empty(plan.Operations);
        Assert.Equal(2, plan.Warnings.Count);
        Assert.Contains(plan.Warnings, warning => warning.Contains("VersionChanged", StringComparison.Ordinal));
        Assert.Contains(plan.Warnings, warning => warning.Contains("UninstallCommandChanged", StringComparison.Ordinal));
    }
}
