using WinLedger.Core.Registry;
using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;

namespace WinLedger.Rollback.Registry;

public sealed class RegistryRollbackExecutor(IRegistryMutationProvider mutations)
{
    public async Task<IReadOnlyList<RegistryRollbackResult>> ApplyAsync(
        RegistryRollbackPlan plan,
        IReadOnlySet<Guid> selectedOperationIds,
        CancellationToken cancellationToken)
    {
        var results = new List<RegistryRollbackResult>();

        foreach (var operation in plan.Operations.Where(operation => selectedOperationIds.Contains(operation.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ApplyOperationAsync(operation, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<RegistryRollbackResult> ValidateAsync(
        RegistryRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        var current = await mutations.ReadValueAsync(operation.KeyPath, operation.ValueName, cancellationToken)
            .ConfigureAwait(false);

        if (!ValuesMatch(current, operation.ExpectedCurrentValue))
        {
            return new RegistryRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The registry value changed after the comparison snapshot. Rollback was not applied.");
        }

        return new RegistryRollbackResult(
            operation.Id,
            true,
            RollbackValidationState.Valid,
            "The current registry value matches the tracked post-change state.");
    }

    private async Task<RegistryRollbackResult> ApplyOperationAsync(
        RegistryRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(operation, cancellationToken).ConfigureAwait(false);
        if (!validation.Succeeded)
        {
            return validation;
        }

        try
        {
            if (operation.Kind == RollbackOperationKind.DeleteRegistryValue)
            {
                await mutations.DeleteValueAsync(operation.KeyPath, operation.ValueName, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                if (operation.RestoreValue is null)
                {
                    return new RegistryRollbackResult(
                        operation.Id,
                        false,
                        RollbackValidationState.Failed,
                        "Rollback operation does not contain a value to restore.");
                }

                await mutations.SetValueAsync(operation.KeyPath, operation.RestoreValue, cancellationToken)
                    .ConfigureAwait(false);
            }

            return new RegistryRollbackResult(
                operation.Id,
                true,
                RollbackValidationState.Valid,
                "Rollback operation completed.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            return new RegistryRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Failed,
                ex.Message);
        }
    }

    private static bool ValuesMatch(RegistryValueSnapshot? current, RegistryValueSnapshot? expected)
    {
        if (current is null || expected is null)
        {
            return current is null && expected is null;
        }

        return current.ValueType == expected.ValueType &&
               string.Equals(current.SerializedValue, expected.SerializedValue, StringComparison.Ordinal);
    }
}
