using WinLedger.Comparison.Startup;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.Startup;

namespace WinLedger.Tests;

public sealed class StartupSnapshotComparerTests
{
    [Fact]
    public void CompareDetectsCreatedRemovedCommandEnabledAndMetadataChanges()
    {
        var sessionId = Guid.NewGuid();
        var beforeEntry = Entry(
            "StartupFolder|C:\\Startup\\Example.lnk",
            StartupEntrySourceKind.StartupFolder,
            "Example.lnk",
            @"C:\Startup\Example.lnk",
            @"C:\Before\example.exe",
            enabled: true,
            fileSize: 42);
        var removedEntry = Entry(
            "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run|Removed",
            StartupEntrySourceKind.RegistryRun,
            "Removed",
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Removed",
            @"C:\Removed\app.exe",
            enabled: true);
        var afterEntry = beforeEntry with
        {
            Command = @"C:\After\example.exe",
            Enabled = false
        };
        var metadataEntry = Entry(
            "StartupFolder|C:\\Startup\\Metadata.lnk",
            StartupEntrySourceKind.StartupFolder,
            "Metadata.lnk",
            @"C:\Startup\Metadata.lnk",
            @"C:\Metadata\app.exe",
            enabled: true,
            fileSize: 10);
        var changedMetadataEntry = metadataEntry with { FileSize = 12 };
        var createdEntry = Entry(
            "StartupFolder|C:\\Startup\\Created.lnk",
            StartupEntrySourceKind.StartupFolder,
            "Created.lnk",
            @"C:\Startup\Created.lnk",
            @"C:\Created\app.exe",
            enabled: true);

        var result = new StartupSnapshotComparer().Compare(
            Snapshot(sessionId, beforeEntry, removedEntry, metadataEntry),
            Snapshot(sessionId, afterEntry, changedMetadataEntry, createdEntry),
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change =>
            change.Kind == StartupEntryChangeKind.EntryCreated &&
            change.StableId == createdEntry.StableId &&
            change.RollbackAvailability == RollbackAvailability.RequiresConfirmation);
        Assert.Contains(result.Changes, change =>
            change.Kind == StartupEntryChangeKind.EntryRemoved &&
            change.StableId == removedEntry.StableId &&
            change.RollbackAvailability == RollbackAvailability.Unavailable);
        Assert.Contains(result.Changes, change =>
            change.Kind == StartupEntryChangeKind.CommandChanged &&
            change.StableId == beforeEntry.StableId);
        Assert.Contains(result.Changes, change =>
            change.Kind == StartupEntryChangeKind.EnabledChanged &&
            change.StableId == beforeEntry.StableId);
        Assert.Contains(result.Changes, change =>
            change.Kind == StartupEntryChangeKind.MetadataChanged &&
            change.StableId == metadataEntry.StableId);
    }

    [Fact]
    public void CompareRejectsSnapshotsFromDifferentSessions()
    {
        Assert.Throws<ArgumentException>(() => new StartupSnapshotComparer().Compare(
            Snapshot(Guid.NewGuid(), Entry("StartupFolder|C:\\Startup\\Example.lnk")),
            Snapshot(Guid.NewGuid(), Entry("StartupFolder|C:\\Startup\\Example.lnk")),
            DateTimeOffset.UtcNow));
    }

    private static StartupSnapshot Snapshot(Guid sessionId, params StartupEntrySnapshot[] entries)
    {
        return new StartupSnapshot(Guid.NewGuid(), sessionId, "Snapshot", DateTimeOffset.UtcNow, entries, []);
    }

    private static StartupEntrySnapshot Entry(
        string stableId,
        StartupEntrySourceKind source = StartupEntrySourceKind.StartupFolder,
        string name = "Example.lnk",
        string location = @"C:\Startup\Example.lnk",
        string? command = @"C:\Example\app.exe",
        bool enabled = true,
        long? fileSize = null)
    {
        return new StartupEntrySnapshot(
            stableId,
            source,
            name,
            location,
            command,
            enabled,
            source == StartupEntrySourceKind.WindowsService ? "LocalSystem" : null,
            source == StartupEntrySourceKind.ScheduledTask ? "Logon trigger enabled" : "Startup folder entry",
            source.ToString(),
            fileSize,
            fileSize is null ? null : DateTimeOffset.UnixEpoch);
    }
}
