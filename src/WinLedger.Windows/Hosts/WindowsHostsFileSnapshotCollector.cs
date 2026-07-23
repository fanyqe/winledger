using System.Text;
using System.Security.Cryptography;
using WinLedger.Core.Abstractions;
using WinLedger.Core.Hosts;
using WinLedger.Domain.Hosts;

namespace WinLedger.Windows.Hosts;

public sealed class WindowsHostsFileSnapshotCollector(IClock clock) : IHostsFileSnapshotCollector
{
    public Task<HostsFileSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        return CapturePathAsync(sessionId, snapshotName, ResolveHostsFilePath(), clock.UtcNow, cancellationToken);
    }

    internal static async Task<HostsFileSnapshot> CapturePathAsync(
        Guid sessionId,
        string snapshotName,
        string filePath,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = Path.GetFullPath(filePath);
        var warnings = new List<string>();

        try
        {
            if (!File.Exists(normalizedPath))
            {
                warnings.Add($"Hosts file was not found: {normalizedPath}");
                return HostsFileSnapshot.Missing(sessionId, snapshotName, capturedAt, normalizedPath, warnings);
            }

            var bytes = await File.ReadAllBytesAsync(normalizedPath, cancellationToken)
                .ConfigureAwait(false);
            var content = Encoding.UTF8.GetString(bytes);

            return new HostsFileSnapshot(
                Guid.NewGuid(),
                sessionId,
                snapshotName,
                capturedAt,
                normalizedPath,
                true,
                content,
                Convert.ToBase64String(bytes),
                Convert.ToHexString(SHA256.HashData(bytes)),
                bytes.LongLength,
                new DateTimeOffset(File.GetLastWriteTimeUtc(normalizedPath)),
                SplitLines(content),
                warnings);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            warnings.Add($"Hosts file could not be read: {ex.Message}");
            return HostsFileSnapshot.Missing(sessionId, snapshotName, capturedAt, normalizedPath, warnings);
        }
    }

    internal static string ResolveHostsFilePath()
    {
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (string.IsNullOrWhiteSpace(systemRoot))
        {
            systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        }

        if (string.IsNullOrWhiteSpace(systemRoot))
        {
            systemRoot = @"C:\Windows";
        }

        return Path.GetFullPath(Path.Combine(systemRoot, "System32", "drivers", "etc", "hosts"));
    }

    internal static IReadOnlyList<HostsFileLineSnapshot> SplitLines(string content)
    {
        var lines = new List<HostsFileLineSnapshot>();
        using var reader = new StringReader(content);

        var lineNumber = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lines.Add(new HostsFileLineSnapshot(lineNumber, line));
            lineNumber++;
        }

        return lines;
    }
}
