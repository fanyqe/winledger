using WinLedger.Comparison.Registry;
using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;

namespace WinLedger.Tests;

public sealed class RegistrySnapshotComparerTests
{
    [Fact]
    public void CompareDetectsCreatedModifiedRemovedAndTypeChanges()
    {
        var sessionId = Guid.NewGuid();
        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox");
        var before = new RegistrySnapshot(
            Guid.NewGuid(),
            sessionId,
            "Baseline",
            DateTimeOffset.UtcNow,
            Array.Empty<RegistrySnapshotTarget>(),
            [
                new RegistryKeySnapshot(
                    keyPath,
                    [
                        Value("Removed", RegistryValueType.String, "\"before\"", "before"),
                        Value("Modified", RegistryValueType.String, "\"before\"", "before"),
                        Value("TypeChanged", RegistryValueType.String, "\"1\"", "1")
                    ])
            ],
            Array.Empty<string>());
        var after = before with
        {
            Id = Guid.NewGuid(),
            Name = "Comparison",
            Keys =
            [
                new RegistryKeySnapshot(
                    keyPath,
                    [
                        Value("Created", RegistryValueType.String, "\"after\"", "after"),
                        Value("Modified", RegistryValueType.String, "\"after\"", "after"),
                        Value("TypeChanged", RegistryValueType.DWord, "1", "1")
                    ])
            ]
        };

        var comparison = new RegistrySnapshotComparer().Compare(before, after, DateTimeOffset.UtcNow);

        Assert.Contains(comparison.Changes, change => change.Kind == RegistryChangeKind.ValueCreated && change.ValueName == "Created");
        Assert.Contains(comparison.Changes, change => change.Kind == RegistryChangeKind.ValueRemoved && change.ValueName == "Removed");
        Assert.Contains(comparison.Changes, change => change.Kind == RegistryChangeKind.ValueModified && change.ValueName == "Modified");
        Assert.Contains(comparison.Changes, change => change.Kind == RegistryChangeKind.ValueTypeChanged && change.ValueName == "TypeChanged");
        Assert.All(comparison.Changes.Where(change => change.ValueName is not null), change => Assert.Equal(RollbackAvailability.Automatic, change.RollbackAvailability));
    }

    [Fact]
    public void CompareMarksLocalMachineStartupChangeAsPrivilegedAndStartupRelated()
    {
        var sessionId = Guid.NewGuid();
        var keyPath = new RegistryPath(RegistryHiveKind.LocalMachine, @"SYSTEM\CurrentControlSet\Services\Example");
        var before = RegistrySnapshot.Empty(sessionId, "Baseline", DateTimeOffset.UtcNow) with
        {
            Keys = [new RegistryKeySnapshot(keyPath, [])]
        };
        var after = before with
        {
            Id = Guid.NewGuid(),
            Keys =
            [
                new RegistryKeySnapshot(
                    keyPath,
                    [Value("Start", RegistryValueType.DWord, "2", "2")])
            ]
        };

        var comparison = new RegistrySnapshotComparer().Compare(before, after, DateTimeOffset.UtcNow);
        var change = Assert.Single(comparison.Changes);

        Assert.Contains(ChangeAttentionLabel.Privileged, change.Labels);
        Assert.Contains(ChangeAttentionLabel.StartupRelated, change.Labels);
    }

    [Fact]
    public void CompareListsValuesInsideCreatedKeys()
    {
        var sessionId = Guid.NewGuid();
        var keyPath = new RegistryPath(RegistryHiveKind.CurrentUser, @"Software\WinLedger\TestSandbox\NewKey");
        var before = RegistrySnapshot.Empty(sessionId, "Baseline", DateTimeOffset.UtcNow);
        var after = before with
        {
            Id = Guid.NewGuid(),
            Keys =
            [
                new RegistryKeySnapshot(
                    keyPath,
                    [Value("Created", RegistryValueType.String, "\"value\"", "value")])
            ]
        };

        var comparison = new RegistrySnapshotComparer().Compare(before, after, DateTimeOffset.UtcNow);

        Assert.Contains(comparison.Changes, change => change.Kind == RegistryChangeKind.KeyCreated);
        Assert.Contains(comparison.Changes, change => change.Kind == RegistryChangeKind.ValueCreated && change.ValueName == "Created");
    }

    private static RegistryValueSnapshot Value(
        string name,
        RegistryValueType type,
        string serialized,
        string display)
    {
        return new RegistryValueSnapshot(name, type, serialized, display);
    }
}
