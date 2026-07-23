using System.Text.Json;
using WinLedger.Comparison.EnvironmentVariables;
using WinLedger.Core.EnvironmentVariables;
using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.EnvironmentVariables;

namespace WinLedger.Tests;

public sealed class EnvironmentReportExporterTests
{
    [Fact]
    public void ExportJsonIncludesVersionedEnvironmentChangesAndRollbackPlan()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new EnvironmentSnapshotComparer().Compare(
            new EnvironmentSnapshot(Guid.NewGuid(), sessionId, "Before", DateTimeOffset.UtcNow, [], []),
            new EnvironmentSnapshot(Guid.NewGuid(), sessionId, "After", DateTimeOffset.UtcNow, [Variable("CREATED_VAR", "new")], []),
            DateTimeOffset.UtcNow);
        var plan = new EnvironmentRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var json = new EnvironmentReportExporter().ExportJson(comparison, plan);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("changes").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("rollbackPlan").GetArrayLength());
        Assert.Equal(nameof(EnvironmentRollbackOperationKind.DeleteEnvironmentVariable), document.RootElement.GetProperty("rollbackPlan")[0].GetProperty("kind").GetString());
    }

    private static EnvironmentVariableSnapshot Variable(string name, string value)
    {
        return new EnvironmentVariableSnapshot(
            EnvironmentVariableScopeKind.User,
            name,
            value,
            EnvironmentVariableValueType.String,
            [],
            @"HKCU\Environment");
    }
}
