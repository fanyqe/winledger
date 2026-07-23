using WinLedger.Comparison.Hosts;
using WinLedger.Domain.Hosts;
using WinLedger.Domain.Rollback;

namespace WinLedger.Tests;

public sealed class HostsFileSnapshotComparerTests
{
    [Fact]
    public void CompareDetectsLineAdditionsAndRemovals()
    {
        var sessionId = Guid.NewGuid();

        var result = new HostsFileSnapshotComparer().Compare(
            HostsFileTestData.Snapshot(sessionId, "Before", "127.0.0.1 localhost\r\n10.0.0.1 old.example\r\n"),
            HostsFileTestData.Snapshot(sessionId, "After", "127.0.0.1 localhost\r\n10.0.0.2 new.example\r\n"),
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change =>
            change.Kind == HostsFileChangeKind.LineAdded &&
            change.AfterLine?.Text == "10.0.0.2 new.example" &&
            change.Labels.Contains(ChangeAttentionLabel.NetworkRelated) &&
            change.Labels.Contains(ChangeAttentionLabel.SecuritySensitive) &&
            change.RollbackAvailability == RollbackAvailability.RequiresConfirmation);
        Assert.Contains(result.Changes, change =>
            change.Kind == HostsFileChangeKind.LineRemoved &&
            change.BeforeLine?.Text == "10.0.0.1 old.example");
    }

    [Fact]
    public void CompareTracksDuplicateLinesByOccurrence()
    {
        var sessionId = Guid.NewGuid();

        var result = new HostsFileSnapshotComparer().Compare(
            HostsFileTestData.Snapshot(sessionId, "Before", "127.0.0.1 duplicate.test\r\n127.0.0.1 duplicate.test\r\n"),
            HostsFileTestData.Snapshot(sessionId, "After", "127.0.0.1 duplicate.test\r\n"),
            DateTimeOffset.UtcNow);

        var removed = Assert.Single(result.Changes, change => change.Kind == HostsFileChangeKind.LineRemoved);
        Assert.Equal(2, removed.BeforeLine?.LineNumber);
        Assert.Equal("127.0.0.1 duplicate.test", removed.BeforeLine?.Text);
    }

    [Fact]
    public void CompareReportsFileCreatedAndRemoved()
    {
        var sessionId = Guid.NewGuid();

        var created = new HostsFileSnapshotComparer().Compare(
            HostsFileTestData.Missing(sessionId, "Before"),
            HostsFileTestData.Snapshot(sessionId, "After", "127.0.0.1 localhost\r\n"),
            DateTimeOffset.UtcNow);
        var removed = new HostsFileSnapshotComparer().Compare(
            HostsFileTestData.Snapshot(sessionId, "Before", "127.0.0.1 localhost\r\n"),
            HostsFileTestData.Missing(sessionId, "After"),
            DateTimeOffset.UtcNow);

        Assert.Contains(created.Changes, change => change.Kind == HostsFileChangeKind.FileCreated);
        Assert.Contains(removed.Changes, change => change.Kind == HostsFileChangeKind.FileRemoved);
    }

    [Fact]
    public void CompareReportsContentChangeWhenVisibleLinesMatch()
    {
        var sessionId = Guid.NewGuid();

        var result = new HostsFileSnapshotComparer().Compare(
            HostsFileTestData.Snapshot(sessionId, "Before", "127.0.0.1 localhost\r\n"),
            HostsFileTestData.Snapshot(sessionId, "After", "127.0.0.1 localhost\n"),
            DateTimeOffset.UtcNow);

        var change = Assert.Single(result.Changes);
        Assert.Equal(HostsFileChangeKind.ContentChanged, change.Kind);
        Assert.Contains(ChangeAttentionLabel.PotentiallyDestructive, change.Labels);
    }

    [Fact]
    public void CompareRejectsSnapshotsFromDifferentSessions()
    {
        Assert.Throws<ArgumentException>(() => new HostsFileSnapshotComparer().Compare(
            HostsFileTestData.Snapshot(Guid.NewGuid(), "Before", "127.0.0.1 localhost\r\n"),
            HostsFileTestData.Snapshot(Guid.NewGuid(), "After", "127.0.0.1 localhost\r\n"),
            DateTimeOffset.UtcNow));
    }
}
