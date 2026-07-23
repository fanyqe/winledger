using WinLedger.Domain.Rollback;

namespace WinLedger.Domain.ScheduledTasks;

public sealed record ScheduledTaskChange(
    Guid Id,
    ScheduledTaskChangeKind Kind,
    string TaskPath,
    ScheduledTaskDefinitionSnapshot? Before,
    ScheduledTaskDefinitionSnapshot? After,
    string Summary,
    IReadOnlySet<ChangeAttentionLabel> Labels,
    RollbackAvailability RollbackAvailability)
{
    public string TargetDisplayName => TaskPath;
}
