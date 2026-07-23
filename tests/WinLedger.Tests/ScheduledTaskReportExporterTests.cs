using System.Text.Json;
using WinLedger.Comparison.ScheduledTasks;
using WinLedger.Core.ScheduledTasks;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.ScheduledTasks;
using WinLedger.Rollback.ScheduledTasks;

namespace WinLedger.Tests;

public sealed class ScheduledTaskReportExporterTests
{
    [Fact]
    public void ExportJsonIncludesVersionedScheduledTaskChangesAndRollbackPlan()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new ScheduledTaskSnapshotComparer().Compare(
            new ScheduledTaskSnapshot(Guid.NewGuid(), sessionId, "Before", DateTimeOffset.UtcNow, [], []),
            new ScheduledTaskSnapshot(Guid.NewGuid(), sessionId, "After", DateTimeOffset.UtcNow, [TaskSnapshot(@"\WinLedger\CreatedTask")], []),
            DateTimeOffset.UtcNow);
        var plan = new ScheduledTaskRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var json = new ScheduledTaskReportExporter().ExportJson(comparison, plan);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("changes").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("rollbackPlan").GetArrayLength());
        Assert.Equal(nameof(ScheduledTaskRollbackOperationKind.DeleteScheduledTask), document.RootElement.GetProperty("rollbackPlan")[0].GetProperty("kind").GetString());
    }

    private static ScheduledTaskDefinitionSnapshot TaskSnapshot(string path)
    {
        return new ScheduledTaskDefinitionSnapshot(
            path,
            @"\WinLedger",
            path.Split('\\', StringSplitOptions.RemoveEmptyEntries).Last(),
            true,
            ScheduledTaskStateKind.Ready,
            "SYSTEM",
            ScheduledTaskPrivilegeLevelKind.HighestAvailable,
            [new ScheduledTaskActionSnapshot(ScheduledTaskActionKind.Execute, @"C:\Example\task.exe", null, null, @"C:\Example\task.exe")],
            [new ScheduledTaskTriggerSnapshot(ScheduledTaskTriggerKind.Logon, true, null, null, "Logon trigger enabled")],
            $"<Task path=\"{path}\" />");
    }
}
