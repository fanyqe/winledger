using WinLedger.Core.EnvironmentVariables;
using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Domain.Rollback;

namespace WinLedger.Rollback.EnvironmentVariables;

public sealed class EnvironmentRollbackExecutor(IEnvironmentMutationProvider mutations)
{
    public async Task<IReadOnlyList<EnvironmentRollbackResult>> ApplyAsync(
        EnvironmentRollbackPlan plan,
        IReadOnlySet<Guid> selectedOperationIds,
        CancellationToken cancellationToken)
    {
        var results = new List<EnvironmentRollbackResult>();

        foreach (var operation in plan.Operations.Where(operation => selectedOperationIds.Contains(operation.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ApplyOperationAsync(operation, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<EnvironmentRollbackResult> ValidateAsync(
        EnvironmentRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        var current = await mutations.ReadVariableAsync(operation.Scope, operation.Name, cancellationToken)
            .ConfigureAwait(false);

        if (!VariablesMatch(current, operation.ExpectedCurrentVariable))
        {
            return new EnvironmentRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The environment variable changed after the comparison snapshot. Rollback was not applied.");
        }

        return new EnvironmentRollbackResult(
            operation.Id,
            true,
            RollbackValidationState.Valid,
            "The current environment variable matches the tracked post-change state.");
    }

    private async Task<EnvironmentRollbackResult> ApplyOperationAsync(
        EnvironmentRollbackOperation operation,
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
                case EnvironmentRollbackOperationKind.DeleteEnvironmentVariable:
                    await mutations.DeleteVariableAsync(operation.Scope, operation.Name, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case EnvironmentRollbackOperationKind.SetEnvironmentVariable:
                    if (operation.RestoreVariable is null)
                    {
                        return MissingRestoreValue(operation.Id);
                    }

                    await mutations.SetVariableAsync(operation.RestoreVariable, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                default:
                    return new EnvironmentRollbackResult(
                        operation.Id,
                        false,
                        RollbackValidationState.Failed,
                        $"Unsupported environment rollback operation: {operation.Kind}");
            }

            return new EnvironmentRollbackResult(
                operation.Id,
                true,
                RollbackValidationState.Valid,
                "Rollback operation completed.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            return new EnvironmentRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Failed,
                ex.Message);
        }
    }

    private static bool VariablesMatch(
        EnvironmentVariableSnapshot? current,
        EnvironmentVariableSnapshot? expected)
    {
        if (current is null || expected is null)
        {
            return current is null && expected is null;
        }

        return current.Scope == expected.Scope &&
               string.Equals(current.Name, expected.Name, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.RawValue, expected.RawValue, StringComparison.Ordinal) &&
               current.ValueType == expected.ValueType &&
               current.PathEntries.SequenceEqual(expected.PathEntries, StringComparer.OrdinalIgnoreCase) &&
               string.Equals(current.SourceKey, expected.SourceKey, StringComparison.OrdinalIgnoreCase);
    }

    private static EnvironmentRollbackResult MissingRestoreValue(Guid operationId)
    {
        return new EnvironmentRollbackResult(
            operationId,
            false,
            RollbackValidationState.Failed,
            "Rollback operation does not contain a value to restore.");
    }
}
