using WinLedger.Comparison.ScheduledTasks;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.ScheduledTasks;
using WinLedger.Rollback.ScheduledTasks;

namespace WinLedger.Tests;

public sealed class ScheduledTaskRollbackPlannerTests
{
    [Fact]
    public void CreatePlanAddsOperationsForCreatedTaskAndEnabledChange()
    {
        var sessionId = Guid.NewGuid();
        var before = TaskSnapshot(@"\WinLedger\ExistingTask", false);
        var after = TaskSnapshot(@"\WinLedger\ExistingTask", true);
        var created = TaskSnapshot(@"\WinLedger\CreatedTask", true);
        var comparison = new ScheduledTaskSnapshotComparer().Compare(
            new ScheduledTaskSnapshot(Guid.NewGuid(), sessionId, "Before", DateTimeOffset.UtcNow, [before], []),
            new ScheduledTaskSnapshot(Guid.NewGuid(), sessionId, "After", DateTimeOffset.UtcNow, [after, created], []),
            DateTimeOffset.UtcNow);

        var plan = new ScheduledTaskRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        Assert.Contains(plan.Operations, operation =>
            operation.Kind == ScheduledTaskRollbackOperationKind.DeleteScheduledTask &&
            operation.TaskPath == @"\WinLedger\CreatedTask" &&
            operation.RequiresAdministrator);
        Assert.Contains(plan.Operations, operation =>
            operation.Kind == ScheduledTaskRollbackOperationKind.SetScheduledTaskEnabled &&
            operation.TaskPath == @"\WinLedger\ExistingTask" &&
            operation.RestoreEnabled == false &&
            operation.RequiresAdministrator);
    }

    private static ScheduledTaskDefinitionSnapshot TaskSnapshot(string path, bool enabled)
    {
        return new ScheduledTaskDefinitionSnapshot(
            path,
            @"\WinLedger",
            path.Split('\\', StringSplitOptions.RemoveEmptyEntries).Last(),
            enabled,
            enabled ? ScheduledTaskStateKind.Ready : ScheduledTaskStateKind.Disabled,
            "SYSTEM",
            ScheduledTaskPrivilegeLevelKind.HighestAvailable,
            [new ScheduledTaskActionSnapshot(ScheduledTaskActionKind.Execute, @"C:\Example\task.exe", null, null, @"C:\Example\task.exe")],
            [new ScheduledTaskTriggerSnapshot(ScheduledTaskTriggerKind.Logon, true, null, null, "Logon trigger enabled")],
            $"<Task path=\"{path}\" enabled=\"{enabled}\" />");
    }
}
