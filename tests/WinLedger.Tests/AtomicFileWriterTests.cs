using System.Text;
using WinLedger.Windows.FileSystem;

namespace WinLedger.Tests;

public sealed class AtomicFileWriterTests
{
    [Fact]
    public async Task ReplaceAsyncReplacesExistingFileAndCleansScratchFiles()
    {
        var directory = CreateTempDirectory();
        var target = Path.Combine(directory, "target.txt");
        await File.WriteAllTextAsync(target, "before");
        var lastWriteTimeUtc = DateTimeOffset.Parse(
            "2026-07-24T08:30:00Z",
            System.Globalization.CultureInfo.InvariantCulture);

        await AtomicFileWriter.ReplaceAsync(
            target,
            Encoding.UTF8.GetBytes("after"),
            lastWriteTimeUtc,
            CancellationToken.None);

        Assert.Equal("after", await File.ReadAllTextAsync(target));
        AssertWithinTwoSeconds(lastWriteTimeUtc.UtcDateTime, File.GetLastWriteTimeUtc(target));
        Assert.Empty(Directory.EnumerateFiles(directory, "target.txt.winledger-*"));
    }

    [Fact]
    public async Task ReplaceAsyncCreatesMissingFileAndParentDirectory()
    {
        var directory = CreateTempDirectory();
        var target = Path.Combine(directory, "nested", "target.txt");

        await AtomicFileWriter.ReplaceAsync(
            target,
            Encoding.UTF8.GetBytes("created"),
            null,
            CancellationToken.None);

        Assert.Equal("created", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(target)!, "target.txt.winledger-*"));
    }

    [Fact]
    public async Task ReplaceAsyncLeavesExistingTargetUnchangedWhenCancelledBeforeSwap()
    {
        var directory = CreateTempDirectory();
        var target = Path.Combine(directory, "target.txt");
        await File.WriteAllTextAsync(target, "before");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AtomicFileWriter.ReplaceAsync(
                target,
                Encoding.UTF8.GetBytes("after"),
                null,
                cancellation.Token));

        Assert.Equal("before", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.EnumerateFiles(directory, "target.txt.winledger-*"));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WinLedgerAtomicFileWriterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void AssertWithinTwoSeconds(DateTime expected, DateTime actual)
    {
        Assert.True(
            Math.Abs((expected - actual).TotalSeconds) < 2,
            $"Expected {actual:o} to be within two seconds of {expected:o}.");
    }
}
