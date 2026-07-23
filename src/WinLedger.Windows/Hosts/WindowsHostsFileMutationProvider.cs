using WinLedger.Core.Abstractions;
using WinLedger.Core.Hosts;
using WinLedger.Domain.Hosts;
using WinLedger.Windows.FileSystem;

namespace WinLedger.Windows.Hosts;

public sealed class WindowsHostsFileMutationProvider(IClock clock) : IHostsFileMutationProvider
{
    public Task<HostsFileSnapshot> ReadSnapshotAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var safePath = ValidateHostsPath(filePath);
        return WindowsHostsFileSnapshotCollector.CapturePathAsync(
            Guid.Empty,
            "Current",
            safePath,
            clock.UtcNow,
            cancellationToken);
    }

    public async Task RestoreContentAsync(
        string filePath,
        string contentBase64,
        CancellationToken cancellationToken)
    {
        var safePath = ValidateHostsPath(filePath);
        var bytes = Convert.FromBase64String(contentBase64);
        await AtomicFileWriter.ReplaceAsync(safePath, bytes, null, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task DeleteFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var safePath = ValidateHostsPath(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(safePath))
        {
            File.Delete(safePath);
        }

        return Task.CompletedTask;
    }

    private static string ValidateHostsPath(string filePath)
    {
        var expected = WindowsHostsFileSnapshotCollector.ResolveHostsFilePath();
        var actual = Path.GetFullPath(filePath);

        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Hosts file mutation is restricted to the Windows hosts file path.");
        }

        if (File.Exists(actual) && File.GetAttributes(actual).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException("Hosts file mutation refuses to write through a reparse point.");
        }

        return actual;
    }
}
