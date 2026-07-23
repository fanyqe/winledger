using WinLedger.Core.ScheduledTasks;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.ScheduledTasks;

namespace WinLedger.Rollback.ScheduledTasks;

public sealed class ScheduledTaskRollbackExecutor(IScheduledTaskMutationProvider mutations)
{
    public async Task<IReadOnlyList<ScheduledTaskRollbackResult>> ApplyAsync(
        ScheduledTaskRollbackPlan plan,
        IReadOnlySet<Guid> selectedOperationIds,
        CancellationToken cancellationToken)
    {
        var results = new List<ScheduledTaskRollbackResult>();

        foreach (var operation in plan.Operations.Where(operation => selectedOperationIds.Contains(operation.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ApplyOperationAsync(operation, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<ScheduledTaskRollbackResult> ValidateAsync(
        ScheduledTaskRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        var current = await mutations.ReadTaskAsync(operation.TaskPath, cancellationToken)
            .ConfigureAwait(false);

        if (current is null)
        {
            return new ScheduledTaskRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The scheduled task could not be found. Rollback was not applied.");
        }

        if (!ExpectedTaskMatches(current, operation.ExpectedCurrentTask))
        {
            return new ScheduledTaskRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The scheduled task changed after the comparison snapshot. Rollback was not applied.");
        }

        return new ScheduledTaskRollbackResult(
            operation.Id,
            true,
            RollbackValidationState.Valid,
            "The current scheduled task matches the tracked post-change state.");
    }

    private async Task<ScheduledTaskRollbackResult> ApplyOperationAsync(
        ScheduledTaskRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(operation, cancellationToken).ConfigureAwait(false);
        if (!validation.Succeeded)
        {
            return validation;
        }

        try
        {
            switch (operation.Kind)
            {
                case ScheduledTaskRollbackOperationKind.DeleteScheduledTask:
                    await mutations.DeleteTaskAsync(operation.TaskPath, cancellationToken).ConfigureAwait(false);
                    break;

                case ScheduledTaskRollbackOperationKind.SetScheduledTaskEnabled:
                    if (operation.RestoreEnabled is null)
                    {
                        return MissingRestoreValue(operation.Id);
                    }

                    await mutations.SetEnabledAsync(operation.TaskPath, operation.RestoreEnabled.Value, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                default:
                    return new ScheduledTaskRollbackResult(
                        operation.Id,
                        false,
                        RollbackValidationState.Failed,
                        $"Unsupported scheduled task rollback operation: {operation.Kind}");
            }

            return new ScheduledTaskRollbackResult(
                operation.Id,
                true,
                RollbackValidationState.Valid,
                "Rollback operation completed.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            return new ScheduledTaskRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Failed,
                ex.Message);
        }
    }

    private static bool ExpectedTaskMatches(
        ScheduledTaskDefinitionSnapshot current,
        ScheduledTaskDefinitionSnapshot expected)
    {
        return string.Equals(current.FullPath, expected.FullPath, StringComparison.OrdinalIgnoreCase) &&
               current.Enabled == expected.Enabled &&
               string.Equals(current.RunAsUser, expected.RunAsUser, StringComparison.OrdinalIgnoreCase) &&
               current.PrivilegeLevel == expected.PrivilegeLevel &&
               current.Actions.SequenceEqual(expected.Actions) &&
               current.Triggers.SequenceEqual(expected.Triggers) &&
               string.Equals(NormalizeXml(current.DefinitionXml), NormalizeXml(expected.DefinitionXml), StringComparison.Ordinal);
    }

    private static string NormalizeXml(string xml)
    {
        return xml.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }

    private static ScheduledTaskRollbackResult MissingRestoreValue(Guid operationId)
    {
        return new ScheduledTaskRollbackResult(
            operationId,
            false,
            RollbackValidationState.Failed,
            "Rollback operation does not contain a value to restore.");
    }
}
