using WinLedger.Core.FileSystem;
using WinLedger.Domain.FileSystem;

namespace WinLedger.Windows.FileSystem;

public sealed class WindowsFileSystemMutationProvider : IFileSystemMutationProvider
{
    public Task<FileSystemEntrySnapshot?> ReadEntryAsync(
        string rootPath,
        string path,
        bool calculateHash,
        CancellationToken cancellationToken)
    {
        return WindowsFileSystemSnapshotCollector.ReadEntryAsync(
            rootPath,
            path,
            calculateHash,
            false,
            0,
            true,
            FileSystemSnapshotOptions.DefaultExclusionPatterns,
            cancellationToken);
    }

    public Task DeleteEntryAsync(
        string rootPath,
        string path,
        FileSystemEntryKind kind,
        CancellationToken cancellationToken)
    {
        var safePath = ValidateMutationPath(rootPath, path);
        cancellationToken.ThrowIfCancellationRequested();

        if (kind == FileSystemEntryKind.File)
        {
            if (File.Exists(safePath))
            {
                File.Delete(safePath);
            }

            return Task.CompletedTask;
        }

        if (Directory.Exists(safePath))
        {
            if (Directory.EnumerateFileSystemEntries(safePath).Any())
            {
                throw new InvalidOperationException("Directory rollback refuses to delete a non-empty directory.");
            }

            Directory.Delete(safePath);
        }

        return Task.CompletedTask;
    }

    public async Task RestoreFileContentAsync(
        string rootPath,
        string path,
        string contentBase64,
        DateTimeOffset? lastWriteTimeUtc,
        CancellationToken cancellationToken)
    {
        var safePath = ValidateMutationPath(rootPath, path);
        var bytes = Convert.FromBase64String(contentBase64);
        var parent = Path.GetDirectoryName(safePath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        await File.WriteAllBytesAsync(safePath, bytes, cancellationToken)
            .ConfigureAwait(false);

        if (lastWriteTimeUtc is not null)
        {
            File.SetLastWriteTimeUtc(safePath, lastWriteTimeUtc.Value.UtcDateTime);
        }
    }

    private static string ValidateMutationPath(string rootPath, string path)
    {
        var root = Path.GetFullPath(rootPath);
        var target = Path.GetFullPath(path);

        if (!WindowsFileSystemSnapshotCollector.IsPathUnderRoot(target, root))
        {
            throw new InvalidOperationException("File-system rollback target is outside the monitored root.");
        }

        if (File.Exists(target) || Directory.Exists(target))
        {
            var attributes = File.GetAttributes(target);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException("File-system rollback refuses to mutate a reparse point.");
            }
        }

        var parent = Path.GetDirectoryName(target);
        while (!string.IsNullOrWhiteSpace(parent) &&
               WindowsFileSystemSnapshotCollector.IsPathUnderRoot(parent, root))
        {
            if (Directory.Exists(parent) &&
                File.GetAttributes(parent).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException("File-system rollback refuses to write through a reparse point.");
            }

            if (string.Equals(Path.GetFullPath(parent), root, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            parent = Path.GetDirectoryName(parent);
        }

        return target;
    }
}
