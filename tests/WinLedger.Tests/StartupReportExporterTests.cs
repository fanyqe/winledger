using System.Text.Json;
using WinLedger.Comparison.Startup;
using WinLedger.Core.Startup;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.Startup;
using WinLedger.Rollback.Startup;

namespace WinLedger.Tests;

public sealed class StartupReportExporterTests
{
    [Fact]
    public void ExportJsonIncludesVersionedStartupChangesAndRollbackPlan()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new StartupSnapshotComparer().Compare(
            new StartupSnapshot(Guid.NewGuid(), sessionId, "Before", DateTimeOffset.UtcNow, [], []),
            new StartupSnapshot(Guid.NewGuid(), sessionId, "After", DateTimeOffset.UtcNow, [Entry(@"C:\Startup\Created.lnk")], []),
            DateTimeOffset.UtcNow);
        var plan = new StartupRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var json = new StartupReportExporter().ExportJson(comparison, plan);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("changes").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("rollbackPlan").GetArrayLength());
        Assert.Equal(nameof(StartupRollbackOperationKind.DeleteStartupFolderEntry), document.RootElement.GetProperty("rollbackPlan")[0].GetProperty("kind").GetString());
    }

    private static StartupEntrySnapshot Entry(string location)
    {
        return new StartupEntrySnapshot(
            $"StartupFolder|{location}",
            StartupEntrySourceKind.StartupFolder,
            Path.GetFileName(location),
            location,
            location,
            true,
            null,
            "Startup folder entry",
            "StartupFolder",
            42,
            DateTimeOffset.UnixEpoch);
    }
}
