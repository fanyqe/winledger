using WinLedger.Comparison.Hosts;
using WinLedger.Domain.Hosts;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.Hosts;

namespace WinLedger.Tests;

public sealed class HostsFileRollbackPlannerTests
{
    [Fact]
    public void CreatePlanAddsOneRestoreOperationForMultipleLineChanges()
    {
        var sessionId = Guid.NewGuid();
        var before = HostsFileTestData.Snapshot(sessionId, "Before", "127.0.0.1 localhost\r\n10.0.0.1 old.example\r\n");
        var after = HostsFileTestData.Snapshot(sessionId, "After", "127.0.0.1 localhost\r\n10.0.0.2 new.example\r\n");
        var comparison = new HostsFileSnapshotComparer().Compare(before, after, DateTimeOffset.UtcNow);

        var plan = new HostsFileRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var operation = Assert.Single(plan.Operations);
        Assert.Equal(HostsFileRollbackOperationKind.RestoreHostsFileContent, operation.Kind);
        Assert.Equal(after.ContentBase64, operation.ExpectedCurrentSnapshot.ContentBase64);
        Assert.Equal(before.ContentBase64, operation.RestoreContentBase64);
        Assert.True(operation.RequiresAdministrator);
        Assert.False(operation.RequiresRestart);
    }

    [Fact]
    public void CreatePlanDeletesCreatedHostsFile()
    {
        var sessionId = Guid.NewGuid();
        var before = HostsFileTestData.Missing(sessionId, "Before");
        var after = HostsFileTestData.Snapshot(sessionId, "After", "127.0.0.1 localhost\r\n");
        var comparison = new HostsFileSnapshotComparer().Compare(before, after, DateTimeOffset.UtcNow);

        var plan = new HostsFileRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var operation = Assert.Single(plan.Operations);
        Assert.Equal(HostsFileRollbackOperationKind.DeleteHostsFile, operation.Kind);
        Assert.Equal(after.ContentBase64, operation.ExpectedCurrentSnapshot.ContentBase64);
        Assert.Null(operation.RestoreContentBase64);
        Assert.True(operation.RequiresAdministrator);
    }

    [Fact]
    public void CreatePlanRestoresRemovedHostsFile()
    {
        var sessionId = Guid.NewGuid();
        var before = HostsFileTestData.Snapshot(sessionId, "Before", "127.0.0.1 localhost\r\n");
        var after = HostsFileTestData.Missing(sessionId, "After");
        var comparison = new HostsFileSnapshotComparer().Compare(before, after, DateTimeOffset.UtcNow);

        var plan = new HostsFileRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var operation = Assert.Single(plan.Operations);
        Assert.Equal(HostsFileRollbackOperationKind.RestoreHostsFileContent, operation.Kind);
        Assert.False(operation.ExpectedCurrentSnapshot.Exists);
        Assert.Equal(before.ContentBase64, operation.RestoreContentBase64);
    }

    [Fact]
    public void CreatePlanWarnsWhenRestoreContentIsMissing()
    {
        var sessionId = Guid.NewGuid();
        var before = HostsFileTestData.Snapshot(sessionId, "Before", "127.0.0.1 localhost\r\n") with { ContentBase64 = null };
        var after = HostsFileTestData.Snapshot(sessionId, "After", "127.0.0.1 localhost\r\n10.0.0.2 new.example\r\n");
        var change = new HostsFileChange(
            Guid.NewGuid(),
            HostsFileChangeKind.LineAdded,
            before.FilePath,
            null,
            after.Lines[1],
            before,
            after,
            "Synthetic change",
            new HashSet<ChangeAttentionLabel>(),
            RollbackAvailability.ManualReview);
        var comparison = new HostsFileComparison(Guid.NewGuid(), sessionId, before.Id, after.Id, DateTimeOffset.UtcNow, [change], []);

        var plan = new HostsFileRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.Empty(plan.Operations);
        Assert.Contains(plan.Warnings, warning => warning.Contains("manual review", StringComparison.OrdinalIgnoreCase));
    }
}
