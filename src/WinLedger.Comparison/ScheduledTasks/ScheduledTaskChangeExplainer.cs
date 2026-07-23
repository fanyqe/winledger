using WinLedger.Domain.Rollback;
using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Comparison.ScheduledTasks;

public static class ScheduledTaskChangeExplainer
{
    public static string Summarize(
        ScheduledTaskChangeKind kind,
        ScheduledTaskDefinitionSnapshot? before,
        ScheduledTaskDefinitionSnapshot? after)
    {
        var taskPath = after?.FullPath ?? before?.FullPath ?? "unknown task";

        return kind switch
        {
            ScheduledTaskChangeKind.TaskCreated => $"The scheduled task \"{taskPath}\" was created.",
            ScheduledTaskChangeKind.TaskRemoved => $"The scheduled task \"{taskPath}\" was removed.",
            ScheduledTaskChangeKind.EnabledChanged => $"The scheduled task \"{taskPath}\" enabled state changed from {before?.Enabled} to {after?.Enabled}.",
            ScheduledTaskChangeKind.ActionChanged => $"The scheduled task \"{taskPath}\" actions changed from {DisplayActions(before?.Actions)} to {DisplayActions(after?.Actions)}.",
            ScheduledTaskChangeKind.TriggerChanged => $"The scheduled task \"{taskPath}\" triggers changed from {DisplayTriggers(before?.Triggers)} to {DisplayTriggers(after?.Triggers)}.",
            ScheduledTaskChangeKind.RunAsUserChanged => $"The scheduled task \"{taskPath}\" run-as user changed from {Display(before?.RunAsUser)} to {Display(after?.RunAsUser)}.",
            ScheduledTaskChangeKind.PrivilegeLevelChanged => $"The scheduled task \"{taskPath}\" privilege level changed from {before?.PrivilegeLevel} to {after?.PrivilegeLevel}.",
            ScheduledTaskChangeKind.DefinitionChanged => $"The scheduled task \"{taskPath}\" definition changed.",
            _ => $"The scheduled task \"{taskPath}\" changed."
        };
    }

    public static IReadOnlySet<ChangeAttentionLabel> Classify(
        ScheduledTaskChangeKind kind,
        ScheduledTaskDefinitionSnapshot? before,
        ScheduledTaskDefinitionSnapshot? after,
        RollbackAvailability rollbackAvailability)
    {
        var labels = new HashSet<ChangeAttentionLabel>
        {
            ChangeAttentionLabel.Persistent
        };

        if (IsStartupRelated(before) || IsStartupRelated(after))
        {
            labels.Add(ChangeAttentionLabel.StartupRelated);
        }

        if (IsPrivileged(before) || IsPrivileged(after))
        {
            labels.Add(ChangeAttentionLabel.Privileged);
        }

        if (IsPrivileged(before) || IsPrivileged(after) ||
            kind is ScheduledTaskChangeKind.ActionChanged or ScheduledTaskChangeKind.RunAsUserChanged or ScheduledTaskChangeKind.PrivilegeLevelChanged)
        {
            labels.Add(ChangeAttentionLabel.SecuritySensitive);
        }

        if (kind is ScheduledTaskChangeKind.TaskRemoved or ScheduledTaskChangeKind.ActionChanged or ScheduledTaskChangeKind.TriggerChanged)
        {
            labels.Add(ChangeAttentionLabel.PotentiallyDestructive);
        }

        if (rollbackAvailability is RollbackAvailability.Unavailable or RollbackAvailability.ManualReview)
        {
            labels.Add(ChangeAttentionLabel.RollbackUnavailable);
        }

        return labels;
    }

    public static RollbackAvailability GetRollbackAvailability(ScheduledTaskChangeKind kind)
    {
        return kind switch
        {
            ScheduledTaskChangeKind.TaskCreated or ScheduledTaskChangeKind.EnabledChanged => RollbackAvailability.RequiresConfirmation,
            ScheduledTaskChangeKind.TaskRemoved => RollbackAvailability.ManualReview,
            _ => RollbackAvailability.ManualReview
        };
    }

    private static bool IsStartupRelated(ScheduledTaskDefinitionSnapshot? task)
    {
        return task?.Triggers.Any(trigger => trigger.Kind is ScheduledTaskTriggerKind.Logon or ScheduledTaskTriggerKind.Boot) == true;
    }

    private static bool IsPrivileged(ScheduledTaskDefinitionSnapshot? task)
    {
        if (task is null)
        {
            return false;
        }

        return task.PrivilegeLevel == ScheduledTaskPrivilegeLevelKind.HighestAvailable ||
               string.Equals(task.RunAsUser, "SYSTEM", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(task.RunAsUser, "LOCAL SYSTEM", StringComparison.OrdinalIgnoreCase);
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(empty)" : $"\"{value}\"";
    }

    private static string DisplayActions(IReadOnlyList<ScheduledTaskActionSnapshot>? actions)
    {
        return actions is null || actions.Count == 0
            ? "(none)"
            : string.Join("; ", actions.Select(action => action.Details));
    }

    private static string DisplayTriggers(IReadOnlyList<ScheduledTaskTriggerSnapshot>? triggers)
    {
        return triggers is null || triggers.Count == 0
            ? "(none)"
            : string.Join("; ", triggers.Select(trigger => trigger.Details));
    }
}
