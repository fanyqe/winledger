using System.Text.Json;
using WinLedger.Comparison.Services;
using WinLedger.Core.Services;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.Services;
using WinLedger.Rollback.Services;

namespace WinLedger.Tests;

public sealed class ServiceReportExporterTests
{
    [Fact]
    public void ExportJsonIncludesVersionedServiceChangesAndRollbackPlan()
    {
        var sessionId = Guid.NewGuid();
        var before = Service(ServiceStartModeKind.Automatic);
        var after = Service(ServiceStartModeKind.Disabled);
        var comparison = new ServiceSnapshotComparer().Compare(
            new ServiceSnapshot(Guid.NewGuid(), sessionId, "Before", DateTimeOffset.UtcNow, [before], []),
            new ServiceSnapshot(Guid.NewGuid(), sessionId, "After", DateTimeOffset.UtcNow, [after], []),
            DateTimeOffset.UtcNow);
        var plan = new ServiceRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var json = new ServiceReportExporter().ExportJson(comparison, plan);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("changes").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("rollbackPlan").GetArrayLength());
        Assert.Equal(nameof(ServiceRollbackOperationKind.SetServiceStartMode), document.RootElement.GetProperty("rollbackPlan")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public void ExportTextIncludesReadableServiceRollbackSummary()
    {
        var sessionId = Guid.NewGuid();
        var before = Service(ServiceStartModeKind.Automatic);
        var after = Service(ServiceStartModeKind.Disabled);
        var comparison = new ServiceSnapshotComparer().Compare(
            new ServiceSnapshot(Guid.NewGuid(), sessionId, "Before", DateTimeOffset.UtcNow, [before], []),
            new ServiceSnapshot(Guid.NewGuid(), sessionId, "After", DateTimeOffset.UtcNow, [after], []),
            DateTimeOffset.UtcNow);
        var plan = new ServiceRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var text = new ServiceReportExporter().ExportText(comparison, plan);

        Assert.Contains("WinLedger Services Report", text, StringComparison.Ordinal);
        Assert.Contains("Changes: 1", text, StringComparison.Ordinal);
        Assert.Contains("Rollback operations: 1", text, StringComparison.Ordinal);
        Assert.Contains("ExampleService", text, StringComparison.Ordinal);
        Assert.Contains("SetServiceStartMode", text, StringComparison.Ordinal);
    }

    private static WindowsServiceSnapshot Service(ServiceStartModeKind startMode)
    {
        return new WindowsServiceSnapshot(
            "ExampleService",
            "Example Service",
            startMode,
            @"C:\Example\service.exe",
            "LocalSystem",
            ServiceStateKind.Running,
            false,
            [],
            null);
    }
}
