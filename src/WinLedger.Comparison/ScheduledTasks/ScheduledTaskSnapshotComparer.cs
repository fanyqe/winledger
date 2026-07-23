using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Comparison.ScheduledTasks;

public sealed class ScheduledTaskSnapshotComparer
{
    public ScheduledTaskComparison Compare(ScheduledTaskSnapshot baseline, ScheduledTaskSnapshot comparison, DateTimeOffset comparedAt)
    {
        if (baseline.SessionId != comparison.SessionId)
        {
            throw new ArgumentException("Scheduled task snapshots belong to different sessions.", nameof(comparison));
        }

        var changes = new List<ScheduledTaskChange>();
        var baselineTasks = baseline.Tasks.ToDictionary(task => task.FullPath, StringComparer.OrdinalIgnoreCase);
        var comparisonTasks = comparison.Tasks.ToDictionary(task => task.FullPath, StringComparer.OrdinalIgnoreCase);

        foreach (var (taskPath, after) in comparisonTasks)
        {
            if (!baselineTasks.TryGetValue(taskPath, out var before))
            {
                changes.Add(CreateChange(ScheduledTaskChangeKind.TaskCreated, taskPath, null, after));
                continue;
            }

            AddTaskPropertyChanges(taskPath, before, after, changes);
        }

        foreach (var (taskPath, before) in baselineTasks)
        {
            if (!comparisonTasks.ContainsKey(taskPath))
            {
                changes.Add(CreateChange(ScheduledTaskChangeKind.TaskRemoved, taskPath, before, null));
            }
        }

        return new ScheduledTaskComparison(
            Guid.NewGuid(),
            baseline.SessionId,
            baseline.Id,
            comparison.Id,
            comparedAt,
            changes.OrderBy(change => change.TargetDisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(change => change.Kind).ToArray(),
            baseline.Warnings.Concat(comparison.Warnings).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void AddTaskPropertyChanges(
        string taskPath,
        ScheduledTaskDefinitionSnapshot before,
        ScheduledTaskDefinitionSnapshot after,
        List<ScheduledTaskChange> changes)
    {
        if (before.Enabled != after.Enabled)
        {
            changes.Add(CreateChange(ScheduledTaskChangeKind.EnabledChanged, taskPath, before, after));
        }

        if (!ActionsMatch(before.Actions, after.Actions))
        {
            changes.Add(CreateChange(ScheduledTaskChangeKind.ActionChanged, taskPath, before, after));
        }

        if (!TriggersMatch(before.Triggers, after.Triggers))
        {
            changes.Add(CreateChange(ScheduledTaskChangeKind.TriggerChanged, taskPath, before, after));
        }

        if (!string.Equals(before.RunAsUser, after.RunAsUser, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(CreateChange(ScheduledTaskChangeKind.RunAsUserChanged, taskPath, before, after));
        }

        if (before.PrivilegeLevel != after.PrivilegeLevel)
        {
            changes.Add(CreateChange(ScheduledTaskChangeKind.PrivilegeLevelChanged, taskPath, before, after));
        }

        if (!string.Equals(NormalizeXml(before.DefinitionXml), NormalizeXml(after.DefinitionXml), StringComparison.Ordinal) &&
            !changes.Any(change => change.TaskPath.Equals(taskPath, StringComparison.OrdinalIgnoreCase)))
        {
            changes.Add(CreateChange(ScheduledTaskChangeKind.DefinitionChanged, taskPath, before, after));
        }
    }

    private static ScheduledTaskChange CreateChange(
        ScheduledTaskChangeKind kind,
        string taskPath,
        ScheduledTaskDefinitionSnapshot? before,
        ScheduledTaskDefinitionSnapshot? after)
    {
        var availability = ScheduledTaskChangeExplainer.GetRollbackAvailability(kind);

        return new ScheduledTaskChange(
            Guid.NewGuid(),
            kind,
            taskPath,
            before,
            after,
            ScheduledTaskChangeExplainer.Summarize(kind, before, after),
            ScheduledTaskChangeExplainer.Classify(kind, before, after, availability),
            availability);
    }

    private static bool ActionsMatch(IReadOnlyList<ScheduledTaskActionSnapshot> before, IReadOnlyList<ScheduledTaskActionSnapshot> after)
    {
        return before.SequenceEqual(after);
    }

    private static bool TriggersMatch(IReadOnlyList<ScheduledTaskTriggerSnapshot> before, IReadOnlyList<ScheduledTaskTriggerSnapshot> after)
    {
        return before.SequenceEqual(after);
    }

    private static string NormalizeXml(string xml)
    {
        return xml.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }
}
