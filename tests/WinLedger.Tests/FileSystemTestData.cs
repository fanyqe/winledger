using System.Text;
using WinLedger.Domain.FileSystem;

namespace WinLedger.Tests;

internal static class FileSystemTestData
{
    public const string Root = @"C:\WinLedgerRoot";

    public static FileSystemSnapshot Snapshot(Guid sessionId, params FileSystemEntrySnapshot[] entries)
    {
        return new FileSystemSnapshot(
            Guid.NewGuid(),
            sessionId,
            "Files",
            DateTimeOffset.UtcNow,
            FileSystemSnapshotOptions.ForRoots(Root),
            entries,
            []);
    }

    public static FileSystemEntrySnapshot File(
        string relativePath,
        string? sha256 = "ABCDEF",
        long sizeBytes = 12,
        bool hasRollbackData = false,
        string? content = null)
    {
        var fullPath = System.IO.Path.Combine(Root, relativePath);
        var backup = hasRollbackData
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes(content ?? "rollback"))
            : null;

        return new FileSystemEntrySnapshot(
            $"{Root}|{relativePath}",
            fullPath,
            Root,
            relativePath,
            FileSystemEntryKind.File,
            sizeBytes,
            DateTimeOffset.Parse("2026-07-23T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-07-23T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            "Archive",
            sha256,
            backup is not null,
            backup,
            false);
    }

    public static FileSystemEntrySnapshot Directory(string relativePath)
    {
        var fullPath = System.IO.Path.Combine(Root, relativePath);
        return new FileSystemEntrySnapshot(
            $"{Root}|{relativePath}",
            fullPath,
            Root,
            relativePath,
            FileSystemEntryKind.Directory,
            null,
            DateTimeOffset.Parse("2026-07-23T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-07-23T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            "Directory",
            null,
            false,
            null,
            false);
    }
}
