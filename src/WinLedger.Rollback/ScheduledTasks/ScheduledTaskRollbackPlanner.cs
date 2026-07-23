using WinLedger.Domain.Rollback;
using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Rollback.ScheduledTasks;

public sealed class ScheduledTaskRollbackPlanner
{
    public ScheduledTaskRollbackPlan CreatePlan(ScheduledTaskComparison comparison, DateTimeOffset createdAt)
    {
        var operations = new List<ScheduledTaskRollbackOperation>();
        var warnings = new List<string>();

        foreach (var change in comparison.Changes)
        {
            switch (change.Kind)
            {
                case ScheduledTaskChangeKind.TaskCreated:
                    if (change.After is null)
                    {
                        warnings.Add($"Scheduled task rollback requires manual review: {change.TargetDisplayName}");
                        break;
                    }

                    operations.Add(new ScheduledTaskRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        ScheduledTaskRollbackOperationKind.DeleteScheduledTask,
                        change.TaskPath,
                        change.After,
                        null,
                        true,
                        false));
                    break;

                case ScheduledTaskChangeKind.EnabledChanged:
                    if (change.Before is null || change.After is null)
                    {
                        warnings.Add($"Scheduled task enabled-state rollback requires manual review: {change.TargetDisplayName}");
                        break;
                    }

                    operations.Add(new ScheduledTaskRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        ScheduledTaskRollbackOperationKind.SetScheduledTaskEnabled,
                        change.TaskPath,
                        change.After,
                        change.Before.Enabled,
                        true,
                        false));
                    break;

                default:
                    warnings.Add($"Unsupported scheduled task rollback change kind: {change.Kind} at {change.TargetDisplayName}");
                    break;
            }
        }

        return new ScheduledTaskRollbackPlan(Guid.NewGuid(), comparison.Id, createdAt, operations, warnings);
    }
}
