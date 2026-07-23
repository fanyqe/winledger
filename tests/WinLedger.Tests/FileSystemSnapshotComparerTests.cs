using WinLedger.Comparison.FileSystem;
using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Rollback;

namespace WinLedger.Tests;

public sealed class FileSystemSnapshotComparerTests
{
    [Fact]
    public void CompareDetectsCreatedDeletedModifiedAndHashBackedRenameChanges()
    {
        var sessionId = Guid.NewGuid();
        var deleted = FileSystemTestData.File("deleted.txt", hasRollbackData: true);
        var beforeModified = FileSystemTestData.File("modified.txt", sha256: "OLD", hasRollbackData: true);
        var afterModified = beforeModified with
        {
            Sha256 = "NEW",
            SizeBytes = 20,
            LastWriteTimeUtc = beforeModified.LastWriteTimeUtc?.AddMinutes(1)
        };
        var beforeRename = FileSystemTestData.File("old-name.txt", sha256: "SAME", sizeBytes: 10);
        var afterRename = FileSystemTestData.File("new-name.txt", sha256: "SAME", sizeBytes: 10);
        var created = FileSystemTestData.File("created.txt", sha256: "CREATED");

        var result = new FileSystemSnapshotComparer().Compare(
            FileSystemTestData.Snapshot(sessionId, deleted, beforeModified, beforeRename),
            FileSystemTestData.Snapshot(sessionId, afterModified, afterRename, created),
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change =>
            change.Kind == FileSystemChangeKind.Created &&
            change.Path.EndsWith("created.txt", StringComparison.OrdinalIgnoreCase) &&
            change.RollbackAvailability == RollbackAvailability.RequiresConfirmation);
        Assert.Contains(result.Changes, change =>
            change.Kind == FileSystemChangeKind.Deleted &&
            change.Path.EndsWith("deleted.txt", StringComparison.OrdinalIgnoreCase) &&
            change.RollbackAvailability == RollbackAvailability.RequiresConfirmation);
        Assert.Contains(result.Changes, change =>
            change.Kind == FileSystemChangeKind.Modified &&
            change.Path.EndsWith("modified.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Changes, change =>
            change.Kind == FileSystemChangeKind.Renamed &&
            change.PreviousPath?.EndsWith("old-name.txt", StringComparison.OrdinalIgnoreCase) == true &&
            change.Path.EndsWith("new-name.txt", StringComparison.OrdinalIgnoreCase) &&
            change.RollbackAvailability == RollbackAvailability.ManualReview);
    }

    [Fact]
    public void CompareRejectsSnapshotsFromDifferentSessions()
    {
        Assert.Throws<ArgumentException>(() => new FileSystemSnapshotComparer().Compare(
            FileSystemTestData.Snapshot(Guid.NewGuid(), FileSystemTestData.File("file.txt")),
            FileSystemTestData.Snapshot(Guid.NewGuid(), FileSystemTestData.File("file.txt")),
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CompareWarnsWhenChangeJournalContinuityIsLost()
    {
        var sessionId = Guid.NewGuid();
        var baseline = FileSystemTestData.Snapshot(sessionId) with
        {
            ChangeJournalStates =
            [
                new FileSystemChangeJournalState(@"C:\", "NTFS", true, 100, 1, 50, 1, 1000, null)
            ]
        };
        var comparison = FileSystemTestData.Snapshot(sessionId) with
        {
            ChangeJournalStates =
            [
                new FileSystemChangeJournalState(@"C:\", "NTFS", true, 100, 1, 90, 60, 1000, null)
            ]
        };

        var result = new FileSystemSnapshotComparer().Compare(baseline, comparison, DateTimeOffset.UtcNow);

        Assert.Contains(result.Warnings, warning => warning.Contains("trimmed", StringComparison.OrdinalIgnoreCase));
    }
}
