using System.Text;
using WinLedger.Core.Abstractions;
using WinLedger.Domain.FileSystem;
using WinLedger.Windows.FileSystem;

namespace WinLedger.Tests;

public sealed class WindowsFileSystemSnapshotCollectorTests
{
    [Fact]
    public async Task CaptureAsyncReadsMetadataHashBackupAndDefaultNoiseExclusions()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WinLedgerFileSystemTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(System.IO.Path.Combine(root, "Cache"));
        var trackedPath = System.IO.Path.Combine(root, "tracked.txt");
        var noisyPath = System.IO.Path.Combine(root, "Cache", "noise.tmp");
        await File.WriteAllTextAsync(trackedPath, "tracked");
        await File.WriteAllTextAsync(noisyPath, "noise");

        var collector = new WindowsFileSystemSnapshotCollector(new FixedClock());
        var options = new FileSystemSnapshotOptions(
            [root],
            [@"\Cache\"],
            false,
            true,
            true,
            1024);

        var snapshot = await collector.CaptureAsync(Guid.NewGuid(), "Baseline", options, CancellationToken.None);

        var tracked = Assert.Single(
            snapshot.Entries,
            entry => entry.Path.EndsWith("tracked.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(FileSystemEntryKind.File, tracked.Kind);
        Assert.NotNull(tracked.Sha256);
        Assert.True(tracked.HasRollbackData);
        Assert.Equal("tracked", Encoding.UTF8.GetString(Convert.FromBase64String(tracked.BackupContentBase64!)));
        Assert.DoesNotContain(snapshot.Entries, entry => entry.Path.EndsWith("noise.tmp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CaptureAsyncCanIncludeAndFlagHighNoisePaths()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WinLedgerFileSystemTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(System.IO.Path.Combine(root, "Cache"));
        var noisyPath = System.IO.Path.Combine(root, "Cache", "noise.tmp");
        await File.WriteAllTextAsync(noisyPath, "noise");

        var collector = new WindowsFileSystemSnapshotCollector(new FixedClock());
        var options = new FileSystemSnapshotOptions(
            [root],
            [@"\Cache\"],
            true,
            false,
            false,
            0);

        var snapshot = await collector.CaptureAsync(Guid.NewGuid(), "Baseline", options, CancellationToken.None);

        Assert.Contains(snapshot.Entries, entry =>
            entry.Path.EndsWith("noise.tmp", StringComparison.OrdinalIgnoreCase) &&
            entry.IsTemporaryOrHighNoise);
    }

    [Fact]
    public async Task CaptureAsyncCalculatesHashesWhenUsingDefaultOptions()
    {
        var root = System.IO.Path.Combine(AppContext.BaseDirectory, "WinLedgerFileSystemTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var trackedPath = System.IO.Path.Combine(root, "tracked.txt");
            await File.WriteAllTextAsync(trackedPath, "tracked");

            var collector = new WindowsFileSystemSnapshotCollector(new FixedClock());
            var snapshot = await collector.CaptureAsync(
                Guid.NewGuid(),
                "Baseline",
                FileSystemSnapshotOptions.ForRoots(root),
                CancellationToken.None);

            var tracked = Assert.Single(
                snapshot.Entries,
                entry => entry.Path.EndsWith("tracked.txt", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(tracked.Sha256);
            Assert.False(tracked.HasRollbackData);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.Parse(
            "2026-07-23T10:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
