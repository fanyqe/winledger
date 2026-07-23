using WinLedger.Comparison.EnvironmentVariables;
using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Domain.Rollback;

namespace WinLedger.Tests;

public sealed class EnvironmentSnapshotComparerTests
{
    [Fact]
    public void CompareDetectsCreatedRemovedValueAndPathEntryChanges()
    {
        var sessionId = Guid.NewGuid();
        var beforePath = Variable("Path", @"C:\One;C:\Two;C:\Removed", EnvironmentVariableScopeKind.Machine);
        var afterPath = Variable("Path", @"C:\Two;C:\One;C:\Added", EnvironmentVariableScopeKind.Machine);
        var beforeSecret = Variable("API_TOKEN", "before-secret", EnvironmentVariableScopeKind.User);
        var afterSecret = Variable("API_TOKEN", "after-secret", EnvironmentVariableScopeKind.User);
        var removed = Variable("REMOVED_VAR", "old", EnvironmentVariableScopeKind.User);
        var created = Variable("CREATED_VAR", "new", EnvironmentVariableScopeKind.User);

        var result = new EnvironmentSnapshotComparer().Compare(
            Snapshot(sessionId, beforePath, beforeSecret, removed),
            Snapshot(sessionId, afterPath, afterSecret, created),
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change =>
            change.Kind == EnvironmentVariableChangeKind.VariableCreated &&
            change.Name == "CREATED_VAR" &&
            change.RollbackAvailability == RollbackAvailability.RequiresConfirmation);
        Assert.Contains(result.Changes, change =>
            change.Kind == EnvironmentVariableChangeKind.VariableRemoved &&
            change.Name == "REMOVED_VAR");
        Assert.Contains(result.Changes, change =>
            change.Kind == EnvironmentVariableChangeKind.ValueChanged &&
            change.Name == "API_TOKEN" &&
            change.Summary.Contains("redacted", StringComparison.OrdinalIgnoreCase) &&
            !change.Summary.Contains("after-secret", StringComparison.Ordinal));
        Assert.Contains(result.Changes, change =>
            change.Kind == EnvironmentVariableChangeKind.PathEntryAdded &&
            change.PathEntry == @"C:\Added");
        Assert.Contains(result.Changes, change =>
            change.Kind == EnvironmentVariableChangeKind.PathEntryRemoved &&
            change.PathEntry == @"C:\Removed");
        Assert.Contains(result.Changes, change =>
            change.Kind == EnvironmentVariableChangeKind.PathEntryReordered &&
            change.PathEntry == @"C:\Two" &&
            change.BeforeIndex == 1 &&
            change.AfterIndex == 0);
        Assert.Contains(result.Changes, change =>
            change.Scope == EnvironmentVariableScopeKind.Machine &&
            change.Labels.Contains(ChangeAttentionLabel.Privileged));
    }

    [Fact]
    public void CompareTracksDuplicatePathEntriesByOccurrence()
    {
        var sessionId = Guid.NewGuid();
        var beforePath = Variable("Path", @"C:\One;C:\Dup;C:\Dup", EnvironmentVariableScopeKind.User);
        var afterPath = Variable("Path", @"C:\One;C:\Dup", EnvironmentVariableScopeKind.User);

        var result = new EnvironmentSnapshotComparer().Compare(
            Snapshot(sessionId, beforePath),
            Snapshot(sessionId, afterPath),
            DateTimeOffset.UtcNow);

        var removed = Assert.Single(result.Changes, change => change.Kind == EnvironmentVariableChangeKind.PathEntryRemoved);
        Assert.Equal(@"C:\Dup", removed.PathEntry);
        Assert.Equal(2, removed.BeforeIndex);
    }

    [Fact]
    public void CompareDoesNotReportReorderWhenOnlyNewPathEntryIsInserted()
    {
        var sessionId = Guid.NewGuid();
        var beforePath = Variable("Path", @"C:\One;C:\Two", EnvironmentVariableScopeKind.User);
        var afterPath = Variable("Path", @"C:\Added;C:\One;C:\Two", EnvironmentVariableScopeKind.User);

        var result = new EnvironmentSnapshotComparer().Compare(
            Snapshot(sessionId, beforePath),
            Snapshot(sessionId, afterPath),
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change => change.Kind == EnvironmentVariableChangeKind.PathEntryAdded);
        Assert.DoesNotContain(result.Changes, change => change.Kind == EnvironmentVariableChangeKind.PathEntryReordered);
    }

    [Fact]
    public void CompareRejectsSnapshotsFromDifferentSessions()
    {
        Assert.Throws<ArgumentException>(() => new EnvironmentSnapshotComparer().Compare(
            Snapshot(Guid.NewGuid(), Variable("Path", @"C:\One", EnvironmentVariableScopeKind.User)),
            Snapshot(Guid.NewGuid(), Variable("Path", @"C:\One", EnvironmentVariableScopeKind.User)),
            DateTimeOffset.UtcNow));
    }

    private static EnvironmentSnapshot Snapshot(Guid sessionId, params EnvironmentVariableSnapshot[] variables)
    {
        return new EnvironmentSnapshot(Guid.NewGuid(), sessionId, "Snapshot", DateTimeOffset.UtcNow, variables, []);
    }

    private static EnvironmentVariableSnapshot Variable(
        string name,
        string value,
        EnvironmentVariableScopeKind scope)
    {
        return new EnvironmentVariableSnapshot(
            scope,
            name,
            value,
            EnvironmentVariableValueType.ExpandString,
            string.Equals(name, "Path", StringComparison.OrdinalIgnoreCase)
                ? value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>(),
            scope == EnvironmentVariableScopeKind.User
                ? @"HKCU\Environment"
                : @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
    }
}
