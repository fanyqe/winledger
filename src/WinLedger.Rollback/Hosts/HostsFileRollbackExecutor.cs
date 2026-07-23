using WinLedger.Core.Hosts;
using WinLedger.Domain.Hosts;
using WinLedger.Domain.Rollback;

namespace WinLedger.Rollback.Hosts;

public sealed class HostsFileRollbackExecutor(IHostsFileMutationProvider mutations)
{
    public async Task<IReadOnlyList<HostsFileRollbackResult>> ApplyAsync(
        HostsFileRollbackPlan plan,
        IReadOnlySet<Guid> selectedOperationIds,
        CancellationToken cancellationToken)
    {
        var results = new List<HostsFileRollbackResult>();

        foreach (var operation in plan.Operations.Where(operation => selectedOperationIds.Contains(operation.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ApplyOperationAsync(operation, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<HostsFileRollbackResult> ValidateAsync(
        HostsFileRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        var current = await mutations.ReadSnapshotAsync(operation.FilePath, cancellationToken)
            .ConfigureAwait(false);

        if (!SnapshotsMatch(current, operation.ExpectedCurrentSnapshot))
        {
            return new HostsFileRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The hosts file changed after the comparison snapshot. Rollback was not applied.");
        }

        return new HostsFileRollbackResult(
            operation.Id,
            true,
            RollbackValidationState.Valid,
            "The current hosts file matches the tracked post-change state.");
    }

    private async Task<HostsFileRollbackResult> ApplyOperationAsync(
        HostsFileRollbackOperation operation,
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
                case HostsFileRollbackOperationKind.RestoreHostsFileContent:
                    if (operation.RestoreContentBase64 is null)
                    {
                        return MissingRestoreContent(operation.Id);
                    }

                    await mutations.RestoreContentAsync(operation.FilePath, operation.RestoreContentBase64, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case HostsFileRollbackOperationKind.DeleteHostsFile:
                    await mutations.DeleteFileAsync(operation.FilePath, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                default:
                    return new HostsFileRollbackResult(
                        operation.Id,
                        false,
                        RollbackValidationState.Failed,
                        $"Unsupported hosts file rollback operation: {operation.Kind}");
            }

            return new HostsFileRollbackResult(
                operation.Id,
                true,
                RollbackValidationState.Valid,
                "Rollback operation completed.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            return new HostsFileRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Failed,
                ex.Message);
        }
    }

    private static bool SnapshotsMatch(HostsFileSnapshot current, HostsFileSnapshot expected)
    {
        return string.Equals(current.FilePath, expected.FilePath, StringComparison.OrdinalIgnoreCase) &&
               current.Exists == expected.Exists &&
               string.Equals(current.Content, expected.Content, StringComparison.Ordinal) &&
               string.Equals(current.ContentBase64, expected.ContentBase64, StringComparison.Ordinal) &&
               string.Equals(current.ContentSha256, expected.ContentSha256, StringComparison.OrdinalIgnoreCase) &&
               current.Length == expected.Length &&
               current.Lines.Select(line => (line.LineNumber, line.Text))
                   .SequenceEqual(expected.Lines.Select(line => (line.LineNumber, line.Text)));
    }

    private static HostsFileRollbackResult MissingRestoreContent(Guid operationId)
    {
        return new HostsFileRollbackResult(
            operationId,
            false,
            RollbackValidationState.Failed,
            "Rollback operation does not contain hosts file content to restore.");
    }
}
