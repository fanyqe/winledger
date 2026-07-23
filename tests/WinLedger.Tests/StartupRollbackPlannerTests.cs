using WinLedger.Comparison.Startup;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.Startup;
using WinLedger.Rollback.Startup;

namespace WinLedger.Tests;

public sealed class StartupRollbackPlannerTests
{
    [Fact]
    public void CreatePlanAddsOperationOnlyForCreatedStartupFolderEntry()
    {
        var sessionId = Guid.NewGuid();
        var startupFolderEntry = Entry(
            "StartupFolder|C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\Created.lnk",
            StartupEntrySourceKind.StartupFolder,
            "Created.lnk",
            @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup\Created.lnk");
        var registryEntry = Entry(
            "HKLM64\\Software\\Microsoft\\Windows\\CurrentVersion\\Run|Created",
            StartupEntrySourceKind.RegistryRun,
            "Created",
            @"HKLM64\Software\Microsoft\Windows\CurrentVersion\Run\Created");
        var comparison = new StartupSnapshotComparer().Compare(
            new StartupSnapshot(Guid.NewGuid(), sessionId, "Before", DateTimeOffset.UtcNow, [], []),
            new StartupSnapshot(Guid.NewGuid(), sessionId, "After", DateTimeOffset.UtcNow, [startupFolderEntry, registryEntry], []),
            DateTimeOffset.UtcNow);

        var plan = new StartupRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var operation = Assert.Single(plan.Operations);
        Assert.Equal(StartupRollbackOperationKind.DeleteStartupFolderEntry, operation.Kind);
        Assert.Equal(startupFolderEntry.Location, operation.ExpectedCurrentEntry.Location);
        Assert.True(operation.RequiresAdministrator);
        Assert.False(operation.RequiresRestart);
        Assert.Contains(plan.Warnings, warning => warning.Contains("native subsystem review", StringComparison.OrdinalIgnoreCase));
    }

    private static StartupEntrySnapshot Entry(
        string stableId,
        StartupEntrySourceKind source,
        string name,
        string location)
    {
        return new StartupEntrySnapshot(
            stableId,
            source,
            name,
            location,
            @"C:\Example\app.exe",
            true,
            null,
            "Run at user sign-in",
            source.ToString(),
            null,
            null);
    }
}
