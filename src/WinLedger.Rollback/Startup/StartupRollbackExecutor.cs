using WinLedger.Core.Startup;
using WinLedger.Domain.Rollback;
using WinLedger.Domain.Startup;

namespace WinLedger.Rollback.Startup;

public sealed class StartupRollbackExecutor(IStartupMutationProvider mutations)
{
    public async Task<IReadOnlyList<StartupRollbackResult>> ApplyAsync(
        StartupRollbackPlan plan,
        IReadOnlySet<Guid> selectedOperationIds,
        CancellationToken cancellationToken)
    {
        var results = new List<StartupRollbackResult>();

        foreach (var operation in plan.Operations.Where(operation => selectedOperationIds.Contains(operation.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ApplyOperationAsync(operation, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<StartupRollbackResult> ValidateAsync(
        StartupRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        if (operation.ExpectedCurrentEntry.Source != StartupEntrySourceKind.StartupFolder)
        {
            return new StartupRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Failed,
                "Startup rollback operation is not supported for this source.");
        }

        var current = await mutations.ReadStartupFolderEntryAsync(operation.ExpectedCurrentEntry.Location, cancellationToken)
            .ConfigureAwait(false);

        if (current is null)
        {
            return new StartupRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The startup folder entry could not be found. Rollback was not applied.");
        }

        if (!EntryMatches(current, operation.ExpectedCurrentEntry))
        {
            return new StartupRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The startup folder entry changed after the comparison snapshot. Rollback was not applied.");
        }

        return new StartupRollbackResult(
            operation.Id,
            true,
            RollbackValidationState.Valid,
            "The current startup folder entry matches the tracked post-change state.");
    }

    private async Task<StartupRollbackResult> ApplyOperationAsync(
        StartupRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(operation, cancellationToken).ConfigureAwait(false);
        if (!validation.Succeeded)
        {
            return validation;
        }

        try
        {
            await mutations.DeleteStartupFolderEntryAsync(operation.ExpectedCurrentEntry.Location, cancellationToken)
                .ConfigureAwait(false);

            return new StartupRollbackResult(
                operation.Id,
                true,
                RollbackValidationState.Valid,
                "Rollback operation completed.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            return new StartupRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Failed,
                ex.Message);
        }
    }

    private static bool EntryMatches(StartupEntrySnapshot current, StartupEntrySnapshot expected)
    {
        return current.Source == expected.Source &&
               string.Equals(current.StableId, expected.StableId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.Name, expected.Name, StringComparison.Ordinal) &&
               string.Equals(current.Location, expected.Location, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.Command, expected.Command, StringComparison.OrdinalIgnoreCase) &&
               current.Enabled == expected.Enabled &&
               string.Equals(current.RunAsUser, expected.RunAsUser, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.TriggerDescription, expected.TriggerDescription, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(current.SourceSubsystem, expected.SourceSubsystem, StringComparison.Ordinal) &&
               current.FileSize == expected.FileSize &&
               current.LastWriteTimeUtc == expected.LastWriteTimeUtc;
    }
}
