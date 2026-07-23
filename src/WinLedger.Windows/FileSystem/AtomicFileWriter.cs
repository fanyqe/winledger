namespace WinLedger.Windows.FileSystem;

public static class AtomicFileWriter
{
    private const int BufferSize = 81920;

    public static async Task ReplaceAsync(
        string targetPath,
        byte[] content,
        DateTimeOffset? lastWriteTimeUtc,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(targetPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Atomic file replacement requires a directory-qualified target path.");
        }

        Directory.CreateDirectory(directory);

        var scratchId = Guid.NewGuid().ToString("N");
        var fileName = Path.GetFileName(fullPath);
        var tempPath = Path.Combine(directory, $"{fileName}.winledger-{scratchId}.tmp");
        var backupPath = Path.Combine(directory, $"{fileName}.winledger-{scratchId}.bak");

        try
        {
            await WriteTempFileAsync(tempPath, content, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(fullPath))
            {
                File.Replace(tempPath, fullPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }

            if (lastWriteTimeUtc is not null)
            {
                File.SetLastWriteTimeUtc(fullPath, lastWriteTimeUtc.Value.UtcDateTime);
            }
        }
        finally
        {
            TryDeleteFile(tempPath);
            TryDeleteFile(backupPath);
        }
    }

    private static async Task WriteTempFileAsync(
        string tempPath,
        byte[] content,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            tempPath,
            new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.CreateNew,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                Share = FileShare.None,
                BufferSize = BufferSize
            });

        await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
        }
    }
}
