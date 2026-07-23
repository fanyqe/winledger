using WinLedger.Core.FileSystem;
using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Rollback;

namespace WinLedger.Rollback.FileSystem;

public sealed class FileSystemRollbackExecutor(IFileSystemMutationProvider mutations)
{
    public async Task<IReadOnlyList<FileSystemRollbackResult>> ApplyAsync(
        FileSystemRollbackPlan plan,
        IReadOnlySet<Guid> selectedOperationIds,
        CancellationToken cancellationToken)
    {
        var results = new List<FileSystemRollbackResult>();

        foreach (var operation in plan.Operations.Where(operation => selectedOperationIds.Contains(operation.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ApplyOperationAsync(operation, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<FileSystemRollbackResult> ValidateAsync(
        FileSystemRollbackOperation operation,
        CancellationToken cancellationToken)
    {
        var current = await mutations.ReadEntryAsync(
            operation.RootPath,
            operation.TargetPath,
            operation.ExpectedCurrentEntry?.Sha256 is not null,
            cancellationToken).ConfigureAwait(false);

        if (!CurrentMatches(operation, current))
        {
            return new FileSystemRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Conflict,
                "The file-system entry changed after the comparison snapshot. Rollback was not applied.");
        }

        return new FileSystemRollbackResult(
            operation.Id,
            true,
            RollbackValidationState.Valid,
            "The current file-system entry matches the tracked post-change state.");
    }

    private async Task<FileSystemRollbackResult> ApplyOperationAsync(
        FileSystemRollbackOperation operation,
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
                case FileSystemRollbackOperationKind.DeleteCreatedEntry:
                    await mutations.DeleteEntryAsync(
                        operation.RootPath,
                        operation.TargetPath,
                        operation.EntryKind,
                        cancellationToken).ConfigureAwait(false);
                    break;

                case FileSystemRollbackOperationKind.RestoreDeletedFile:
                case FileSystemRollbackOperationKind.RestoreModifiedFile:
                    if (operation.RestoreContentBase64 is null)
                    {
                        return new FileSystemRollbackResult(
                            operation.Id,
                            false,
                            RollbackValidationState.Failed,
                            "Rollback operation does not contain file content to restore.");
                    }

                    await mutations.RestoreFileContentAsync(
                        operation.RootPath,
                        operation.TargetPath,
                        operation.RestoreContentBase64,
                        operation.RestoreEntry?.LastWriteTimeUtc,
                        cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    return new FileSystemRollbackResult(
                        operation.Id,
                        false,
                        RollbackValidationState.Failed,
                        $"Unsupported file-system rollback operation: {operation.Kind}");
            }

            return new FileSystemRollbackResult(
                operation.Id,
                true,
                RollbackValidationState.Valid,
                "Rollback operation completed.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            return new FileSystemRollbackResult(
                operation.Id,
                false,
                RollbackValidationState.Failed,
                ex.Message);
        }
    }

    private static bool CurrentMatches(FileSystemRollbackOperation operation, FileSystemEntrySnapshot? current)
    {
        if (operation.ExpectedCurrentEntry is null)
        {
            return current is null;
        }

        if (current is null)
        {
            return false;
        }

        return current.Kind == operation.ExpectedCurrentEntry.Kind &&
               string.Equals(current.Path, operation.ExpectedCurrentEntry.Path, StringComparison.OrdinalIgnoreCase) &&
               current.SizeBytes == operation.ExpectedCurrentEntry.SizeBytes &&
               current.LastWriteTimeUtc == operation.ExpectedCurrentEntry.LastWriteTimeUtc &&
               string.Equals(current.Attributes, operation.ExpectedCurrentEntry.Attributes, StringComparison.Ordinal) &&
               string.Equals(current.Sha256, operation.ExpectedCurrentEntry.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
