using WinLedger.Core.Startup;
using WinLedger.Domain.Startup;

namespace WinLedger.Windows.Startup;

public sealed class WindowsStartupMutationProvider : IStartupMutationProvider
{
    public Task<StartupEntrySnapshot?> ReadStartupFolderEntryAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(IsAllowedStartupFolderEntryPath(filePath)
            ? StartupFolderEntryReader.ReadFile(filePath)
            : null);
    }

    public Task DeleteStartupFolderEntryAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsAllowedStartupFolderEntryPath(filePath))
        {
            throw new InvalidOperationException("Startup folder rollback can only modify known Windows startup folder entries.");
        }

        if (!File.Exists(filePath))
        {
            return Task.CompletedTask;
        }

        File.Delete(filePath);
        return Task.CompletedTask;
    }

    private static bool IsAllowedStartupFolderEntryPath(string filePath)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }

        return StartupFolders()
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Select(Path.GetFullPath)
            .Any(folder => IsChildPath(folder, fullPath));
    }

    private static IEnumerable<string> StartupFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
    }

    private static bool IsChildPath(string folderPath, string candidatePath)
    {
        var normalizedFolder = Path.TrimEndingDirectorySeparator(folderPath) + Path.DirectorySeparatorChar;
        return candidatePath.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase);
    }
}
