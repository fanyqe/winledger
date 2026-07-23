using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Domain.Rollback;

public sealed record ScheduledTaskRollbackOperation(
    Guid Id,
    Guid ChangeId,
    ScheduledTaskRollbackOperationKind Kind,
    string TaskPath,
    ScheduledTaskDefinitionSnapshot ExpectedCurrentTask,
    bool? RestoreEnabled,
    bool RequiresAdministrator,
    bool RequiresRestart)
{
    public string TargetDisplayName => TaskPath;
}
