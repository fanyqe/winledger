using System.Security.Cryptography;
using WinLedger.Core.Abstractions;
using WinLedger.Core.FileSystem;
using WinLedger.Domain.FileSystem;

namespace WinLedger.Windows.FileSystem;

public sealed class WindowsFileSystemSnapshotCollector(IClock clock) : IFileSystemSnapshotCollector
{
    public async Task<FileSystemSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        FileSystemSnapshotOptions options,
        CancellationToken cancellationToken)
    {
        var normalizedOptions = NormalizeOptions(options);
        var entries = new List<FileSystemEntrySnapshot>();
        var warnings = new List<string>();
        var changeJournalStatesByVolume = new Dictionary<string, FileSystemChangeJournalState>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in normalizedOptions.MonitoredRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CaptureChangeJournalState(root, changeJournalStatesByVolume);
            await CaptureRootAsync(root, normalizedOptions, entries, warnings, cancellationToken)
                .ConfigureAwait(false);
        }

        return new FileSystemSnapshot(
            Guid.NewGuid(),
            sessionId,
            snapshotName,
            clock.UtcNow,
            normalizedOptions,
            entries.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).ToArray(),
            warnings.Distinct(StringComparer.Ordinal).ToArray())
        {
            ChangeJournalStates = changeJournalStatesByVolume.Values
                .OrderBy(state => state.VolumeRootPath, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    internal static FileSystemSnapshotOptions NormalizeOptions(FileSystemSnapshotOptions options)
    {
        var roots = options.MonitoredRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.GetFullPath(Environment.ExpandEnvironmentVariables(root)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var exclusions = options.ExclusionPatterns.Count == 0
            ? FileSystemSnapshotOptions.DefaultExclusionPatterns
            : options.ExclusionPatterns;

        return options with
        {
            MonitoredRoots = roots,
            ExclusionPatterns = exclusions,
            BackupSizeLimitBytes = Math.Max(0, options.BackupSizeLimitBytes)
        };
    }

    internal static async Task<FileSystemEntrySnapshot?> ReadEntryAsync(
        string rootPath,
        string path,
        bool calculateHash,
        bool backupSmallFiles,
        long backupSizeLimitBytes,
        bool includeHighNoise,
        IReadOnlyList<string> exclusionPatterns,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(rootPath);
        var normalizedPath = Path.GetFullPath(path);
        if (!IsPathUnderRoot(normalizedPath, root))
        {
            throw new InvalidOperationException("File-system entry path is outside the monitored root.");
        }

        if (!File.Exists(normalizedPath) && !Directory.Exists(normalizedPath))
        {
            return null;
        }

        var isHighNoise = IsHighNoisePath(normalizedPath, exclusionPatterns);
        if (isHighNoise && !includeHighNoise)
        {
            return null;
        }

        return await CreateEntryAsync(
            root,
            normalizedPath,
            calculateHash,
            backupSmallFiles,
            backupSizeLimitBytes,
            isHighNoise,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task CaptureRootAsync(
        string root,
        FileSystemSnapshotOptions options,
        List<FileSystemEntrySnapshot> entries,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root) && !File.Exists(root))
        {
            warnings.Add($"Monitored file-system root was not found: {root}");
            return;
        }

        if (File.Exists(root))
        {
            await TryAddEntryAsync(root, root, options, entries, warnings, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            IEnumerable<string> childPaths;
            try
            {
                childPaths = Directory.EnumerateFileSystemEntries(directory).ToArray();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                warnings.Add($"Directory could not be read: {directory} - {ex.Message}");
                continue;
            }

            foreach (var childPath in childPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var added = await TryAddEntryAsync(root, childPath, options, entries, warnings, cancellationToken)
                    .ConfigureAwait(false);

                if (added && Directory.Exists(childPath) && !IsReparsePoint(childPath))
                {
                    pending.Push(childPath);
                }
            }
        }
    }

    private static void CaptureChangeJournalState(
        string root,
        Dictionary<string, FileSystemChangeJournalState> statesByVolume)
    {
        if (!Directory.Exists(root) && !File.Exists(root))
        {
            return;
        }

        var state = WindowsChangeJournalReader.CaptureState(root);
        if (!statesByVolume.ContainsKey(state.VolumeRootPath))
        {
            statesByVolume.Add(state.VolumeRootPath, state);
        }
    }

    private static async Task<bool> TryAddEntryAsync(
        string root,
        string path,
        FileSystemSnapshotOptions options,
        List<FileSystemEntrySnapshot> entries,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            var isHighNoise = IsHighNoisePath(path, options.ExclusionPatterns);
            if (isHighNoise && !options.IncludeHighNoise)
            {
                return false;
            }

            var entry = await CreateEntryAsync(
                root,
                path,
                options.CalculateHashes,
                options.BackupSmallFiles,
                options.BackupSizeLimitBytes,
                isHighNoise,
                cancellationToken).ConfigureAwait(false);

            if (entry is not null)
            {
                entries.Add(entry);
            }

            return entry is not null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            warnings.Add($"File-system entry could not be read: {path} - {ex.Message}");
            return false;
        }
    }

    private static async Task<FileSystemEntrySnapshot?> CreateEntryAsync(
        string root,
        string path,
        bool calculateHash,
        bool backupSmallFiles,
        long backupSizeLimitBytes,
        bool isHighNoise,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            var info = new FileInfo(fullPath);
            string? sha256 = null;
            string? backupContentBase64 = null;

            if (calculateHash)
            {
                sha256 = await HashFileAsync(fullPath, cancellationToken).ConfigureAwait(false);
            }

            if (backupSmallFiles && info.Length <= backupSizeLimitBytes)
            {
                var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken)
                    .ConfigureAwait(false);
                backupContentBase64 = Convert.ToBase64String(bytes);
            }

            return new FileSystemEntrySnapshot(
                CreateIdentity(root, fullPath),
                fullPath,
                root,
                GetRelativePath(root, fullPath),
                FileSystemEntryKind.File,
                info.Length,
                ToDateTimeOffset(info.CreationTimeUtc),
                ToDateTimeOffset(info.LastWriteTimeUtc),
                info.Attributes.ToString(),
                sha256,
                backupContentBase64 is not null,
                backupContentBase64,
                isHighNoise);
        }

        if (Directory.Exists(fullPath))
        {
            var info = new DirectoryInfo(fullPath);
            return new FileSystemEntrySnapshot(
                CreateIdentity(root, fullPath),
                fullPath,
                root,
                GetRelativePath(root, fullPath),
                FileSystemEntryKind.Directory,
                null,
                ToDateTimeOffset(info.CreationTimeUtc),
                ToDateTimeOffset(info.LastWriteTimeUtc),
                info.Attributes.ToString(),
                null,
                false,
                null,
                isHighNoise);
        }

        return null;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    internal static bool IsPathUnderRoot(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsHighNoisePath(string path, IReadOnlyList<string> exclusionPatterns)
    {
        var normalized = Path.GetFullPath(path);
        var wrapped = normalized.EndsWith(Path.DirectorySeparatorChar)
            ? normalized
            : normalized + (Directory.Exists(normalized) ? Path.DirectorySeparatorChar : string.Empty);

        return exclusionPatterns.Any(pattern =>
            normalized.Contains(pattern.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ||
            wrapped.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsReparsePoint(string path)
    {
        return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
    }

    private static string CreateIdentity(string root, string path)
    {
        return $"{Path.GetFullPath(root)}|{GetRelativePath(root, path)}";
    }

    private static string GetRelativePath(string root, string path)
    {
        return Path.GetRelativePath(root, path);
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime utcDateTime)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc));
    }
}
