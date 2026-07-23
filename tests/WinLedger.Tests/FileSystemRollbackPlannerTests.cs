using WinLedger.Comparison.FileSystem;
using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.FileSystem;

namespace WinLedger.Tests;

public sealed class FileSystemRollbackPlannerTests
{
    [Fact]
    public void CreatePlanDeletesCreatedEntriesAndRestoresBackedUpFiles()
    {
        var sessionId = Guid.NewGuid();
        var deleted = FileSystemTestData.File("deleted.txt", hasRollbackData: true, content: "deleted");
        var beforeModified = FileSystemTestData.File("modified.txt", sha256: "OLD", hasRollbackData: true, content: "before");
        var afterModified = beforeModified with { Sha256 = "NEW", SizeBytes = 20 };
        var createdDirectory = FileSystemTestData.Directory("created-folder");
        var comparison = new FileSystemSnapshotComparer().Compare(
            FileSystemTestData.Snapshot(sessionId, deleted, beforeModified),
            FileSystemTestData.Snapshot(sessionId, afterModified, createdDirectory),
            DateTimeOffset.UtcNow);

        var plan = new FileSystemRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.Contains(plan.Operations, operation =>
            operation.Kind == FileSystemRollbackOperationKind.DeleteCreatedEntry &&
            operation.EntryKind == FileSystemEntryKind.Directory &&
            operation.TargetPath.EndsWith("created-folder", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Operations, operation =>
            operation.Kind == FileSystemRollbackOperationKind.RestoreDeletedFile &&
            operation.TargetPath.EndsWith("deleted.txt", StringComparison.OrdinalIgnoreCase) &&
            operation.ExpectedCurrentEntry is null &&
            operation.RestoreContentBase64 is not null);
        Assert.Contains(plan.Operations, operation =>
            operation.Kind == FileSystemRollbackOperationKind.RestoreModifiedFile &&
            operation.TargetPath.EndsWith("modified.txt", StringComparison.OrdinalIgnoreCase) &&
            operation.ExpectedCurrentEntry is not null &&
            operation.RestoreContentBase64 is not null);
    }

    [Fact]
    public void CreatePlanWarnsWhenBackupIsUnavailable()
    {
        var sessionId = Guid.NewGuid();
        var deleted = FileSystemTestData.File("deleted.txt", hasRollbackData: false);
        var comparison = new FileSystemSnapshotComparer().Compare(
            FileSystemTestData.Snapshot(sessionId, deleted),
            FileSystemTestData.Snapshot(sessionId),
            DateTimeOffset.UtcNow);

        var plan = new FileSystemRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.Empty(plan.Operations);
        Assert.Contains(plan.Warnings, warning => warning.Contains("no rollback backup", StringComparison.OrdinalIgnoreCase));
    }
}
