using WinLedger.Core.Services;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.Services;

namespace WinLedger.Rollback.Services;

public sealed class ServiceRollbackExecutor(IServiceMutationProvider mutations)
{
    public async Task<IReadOnlyList<ServiceRollbackResult>> ApplyAsync(
        ServiceRollbackPlan plan,
        IReadOnlySet<Guid> selectedOperationIds,
        CancellationToken cancellationToken)
    {
        var results = new List<ServiceRollbackResult>();

        foreach (var operation in plan.Operations.Where(operation => selectedOperationIds.Contains(operation.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ApplyOperationAsync(operation, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<ServiceRollbackResult> ValidateAsync(
        ServiceRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        var current = await mutations.ReadServiceAsync(operation.ServiceName, cancellationToken)
            .ConfigureAwait(false);

        if (current is null)
        {
            return new ServiceRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The service could not be found. Rollback was not applied.");
        }

        if (!ExpectedStateMatches(current, operation))
        {
            return new ServiceRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The service changed after the comparison snapshot. Rollback was not applied.");
        }

        return new ServiceRollbackResult(
            operation.Id,
            true,
            RollbackValidationState.Valid,
            "The current service configuration matches the tracked post-change state.");
    }

    private async Task<ServiceRollbackResult> ApplyOperationAsync(
        ServiceRollbackOperation operation,
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
                case ServiceRollbackOperationKind.SetServiceStartMode:
                    if (operation.RestoreStartMode is null)
                    {
                        return MissingRestoreValue(operation.Id);
                    }

                    await mutations.SetStartModeAsync(operation.ServiceName, operation.RestoreStartMode.Value, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case ServiceRollbackOperationKind.SetServiceDelayedAutoStart:
                    if (operation.RestoreDelayedAutoStart is null)
                    {
                        return MissingRestoreValue(operation.Id);
                    }

                    await mutations.SetDelayedAutoStartAsync(operation.ServiceName, operation.RestoreDelayedAutoStart.Value, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                default:
                    return new ServiceRollbackResult(
                        operation.Id,
                        false,
                        RollbackValidationState.Failed,
                        $"Unsupported service rollback operation: {operation.Kind}");
            }

            return new ServiceRollbackResult(
                operation.Id,
                true,
                RollbackValidationState.Valid,
                "Rollback operation completed.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            return new ServiceRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Failed,
                ex.Message);
        }
    }

    private static bool ExpectedStateMatches(WindowsServiceSnapshot current, ServiceRollbackOperation operation)
    {
        if (!PersistentConfigurationMatches(current, operation.ExpectedCurrentState))
        {
            return false;
        }

        return operation.Kind is ServiceRollbackOperationKind.SetServiceStartMode
            or ServiceRollbackOperationKind.SetServiceDelayedAutoStart;
    }

    private static bool PersistentConfigurationMatches(
        WindowsServiceSnapshot current,
        WindowsServiceSnapshot expected)
    {
        return string.Equals(current.Name, expected.Name, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.DisplayName, expected.DisplayName, StringComparison.Ordinal) &&
               current.StartMode == expected.StartMode &&
               string.Equals(current.ExecutablePath, expected.ExecutablePath, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.ServiceAccount, expected.ServiceAccount, StringComparison.OrdinalIgnoreCase) &&
               current.DelayedAutoStart == expected.DelayedAutoStart &&
               DependenciesMatch(current.Dependencies, expected.Dependencies);
    }

    private static bool DependenciesMatch(IReadOnlyList<string> current, IReadOnlyList<string> expected)
    {
        return current.Order(StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(expected.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
    }

    private static ServiceRollbackResult MissingRestoreValue(Guid operationId)
    {
        return new ServiceRollbackResult(
            operationId,
            false,
            RollbackValidationState.Failed,
            "Rollback operation does not contain a value to restore.");
    }
}
