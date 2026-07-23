using WinLedger.Comparison.ScheduledTasks;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Tests;

public sealed class ScheduledTaskSnapshotComparerTests
{
    [Fact]
    public void CompareDetectsCreatedRemovedAndDefinitionChanges()
    {
        var sessionId = Guid.NewGuid();
        var beforeTask = TaskSnapshot(@"\WinLedger\ExampleTask", true, @"C:\Before\updater.exe", "SYSTEM", ScheduledTaskPrivilegeLevelKind.HighestAvailable, ScheduledTaskTriggerKind.Logon);
        var removedTask = TaskSnapshot(@"\WinLedger\RemovedTask", true, @"C:\Removed\task.exe", "SYSTEM", ScheduledTaskPrivilegeLevelKind.HighestAvailable, ScheduledTaskTriggerKind.Boot);
        var afterTask = TaskSnapshot(@"\WinLedger\ExampleTask", false, @"C:\After\updater.exe", "UserA", ScheduledTaskPrivilegeLevelKind.LeastPrivilege, ScheduledTaskTriggerKind.Daily);
        var createdTask = TaskSnapshot(@"\WinLedger\CreatedTask", true, @"C:\Created\task.exe", "SYSTEM", ScheduledTaskPrivilegeLevelKind.HighestAvailable, ScheduledTaskTriggerKind.Logon);

        var result = new ScheduledTaskSnapshotComparer().Compare(
            Snapshot(sessionId, beforeTask, removedTask),
            Snapshot(sessionId, afterTask, createdTask),
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Changes, change => change.Kind == ScheduledTaskChangeKind.TaskCreated && change.TaskPath == @"\WinLedger\CreatedTask");
        Assert.Contains(result.Changes, change => change.Kind == ScheduledTaskChangeKind.TaskRemoved && change.TaskPath == @"\WinLedger\RemovedTask");
        Assert.Contains(result.Changes, change => change.Kind == ScheduledTaskChangeKind.EnabledChanged && change.RollbackAvailability == RollbackAvailability.RequiresConfirmation);
        Assert.Contains(result.Changes, change => change.Kind == ScheduledTaskChangeKind.ActionChanged);
        Assert.Contains(result.Changes, change => change.Kind == ScheduledTaskChangeKind.TriggerChanged);
        Assert.Contains(result.Changes, change => change.Kind == ScheduledTaskChangeKind.RunAsUserChanged);
        Assert.Contains(result.Changes, change => change.Kind == ScheduledTaskChangeKind.PrivilegeLevelChanged);
    }

    [Fact]
    public void CompareRejectsSnapshotsFromDifferentSessions()
    {
        Assert.Throws<ArgumentException>(() => new ScheduledTaskSnapshotComparer().Compare(
            Snapshot(Guid.NewGuid(), TaskSnapshot(@"\WinLedger\ExampleTask")),
            Snapshot(Guid.NewGuid(), TaskSnapshot(@"\WinLedger\ExampleTask")),
            DateTimeOffset.UtcNow));
    }

    private static ScheduledTaskSnapshot Snapshot(Guid sessionId, params ScheduledTaskDefinitionSnapshot[] tasks)
    {
        return new ScheduledTaskSnapshot(Guid.NewGuid(), sessionId, "Snapshot", DateTimeOffset.UtcNow, tasks, []);
    }

    private static ScheduledTaskDefinitionSnapshot TaskSnapshot(
        string path,
        bool enabled = true,
        string executablePath = @"C:\Example\task.exe",
        string runAsUser = "SYSTEM",
        ScheduledTaskPrivilegeLevelKind privilegeLevel = ScheduledTaskPrivilegeLevelKind.HighestAvailable,
        ScheduledTaskTriggerKind triggerKind = ScheduledTaskTriggerKind.Logon)
    {
        return new ScheduledTaskDefinitionSnapshot(
            path,
            @"\WinLedger",
            path.Split('\\', StringSplitOptions.RemoveEmptyEntries).Last(),
            enabled,
            enabled ? ScheduledTaskStateKind.Ready : ScheduledTaskStateKind.Disabled,
            runAsUser,
            privilegeLevel,
            [new ScheduledTaskActionSnapshot(ScheduledTaskActionKind.Execute, executablePath, "--check", @"C:\Example", $"{executablePath} --check")],
            [new ScheduledTaskTriggerSnapshot(triggerKind, true, null, null, $"{triggerKind} trigger enabled")],
            $"<Task path=\"{path}\" enabled=\"{enabled}\" executable=\"{executablePath}\" runAs=\"{runAsUser}\" privilege=\"{privilegeLevel}\" trigger=\"{triggerKind}\" />");
    }
}
