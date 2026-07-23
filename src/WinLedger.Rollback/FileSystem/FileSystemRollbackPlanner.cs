using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Rollback;

namespace WinLedger.Rollback.FileSystem;

public sealed class FileSystemRollbackPlanner
{
    public FileSystemRollbackPlan CreatePlan(FileSystemComparison comparison, DateTimeOffset createdAt)
    {
        var operations = new List<FileSystemRollbackOperation>();
        var warnings = new List<string>();

        foreach (var change in comparison.Changes)
        {
            switch (change.Kind)
            {
                case FileSystemChangeKind.Created:
                    if (change.After is null)
                    {
                        warnings.Add($"File-system rollback requires manual review: {change.TargetDisplayName}");
                        break;
                    }

                    operations.Add(new FileSystemRollbackOperation(
                        Guid.NewGuid(),
                        change.Id,
                        FileSystemRollbackOperationKind.DeleteCreatedEntry,
                        change.After.RootPath,
                        change.After.Path,
                        change.After.Kind,
                        change.After,
                        null,
                        null,
                        RequiresAdministrator(change.After.Path),
                        false));
                    break;

                case FileSystemChangeKind.Deleted:
                    if (change.Before is { Kind: FileSystemEntryKind.File, HasRollbackData: true, BackupContentBase64: not null })
                    {
                        operations.Add(new FileSystemRollbackOperation(
                            Guid.NewGuid(),
                            change.Id,
                            FileSystemRollbackOperationKind.RestoreDeletedFile,
                            change.Before.RootPath,
                            change.Before.Path,
                            change.Before.Kind,
                            null,
                            change.Before,
                            change.Before.BackupContentBase64,
                            RequiresAdministrator(change.Before.Path),
                            false));
                    }
                    else
                    {
                        warnings.Add($"Deleted file-system entry requires manual review because no rollback backup is available: {change.TargetDisplayName}");
                    }

                    break;

                case FileSystemChangeKind.Modified:
                    if (change.Before is { Kind: FileSystemEntryKind.File, HasRollbackData: true, BackupContentBase64: not null } &&
                        change.After is not null)
                    {
                        operations.Add(new FileSystemRollbackOperation(
                            Guid.NewGuid(),
                            change.Id,
                            FileSystemRollbackOperationKind.RestoreModifiedFile,
                            change.After.RootPath,
                            change.After.Path,
                            change.After.Kind,
                            change.After,
                            change.Before,
                            change.Before.BackupContentBase64,
                            RequiresAdministrator(change.After.Path),
                            false));
                    }
                    else
                    {
                        warnings.Add($"Modified file-system entry requires manual review because no rollback backup is available: {change.TargetDisplayName}");
                    }

                    break;

                default:
                    warnings.Add($"Unsupported file-system rollback change kind: {change.Kind} at {change.TargetDisplayName}");
                    break;
            }
        }

        return new FileSystemRollbackPlan(Guid.NewGuid(), comparison.Id, createdAt, operations, warnings);
    }

    private static bool RequiresAdministrator(string path)
    {
        var normalized = System.IO.Path.GetFullPath(path);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        return IsUnder(normalized, windows) ||
               IsUnder(normalized, programFiles) ||
               IsUnder(normalized, programFilesX86);
    }

    private static bool IsUnder(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var normalizedRoot = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
