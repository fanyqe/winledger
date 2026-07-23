using System.Text;
using WinLedger.Windows.FileSystem;

namespace WinLedger.Tests;

public sealed class WindowsFileSystemMutationProviderTests
{
    [Fact]
    public async Task RestoreFileContentAsyncRestoresContentTimestampAndCleansScratchFiles()
    {
        var root = CreateTempDirectory();
        var target = Path.Combine(root, "target.txt");
        await File.WriteAllTextAsync(target, "current");
        var restoreTimeUtc = DateTimeOffset.Parse(
            "2026-07-24T09:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture);
        var provider = new WindowsFileSystemMutationProvider();

        await provider.RestoreFileContentAsync(
            root,
            target,
            Convert.ToBase64String(Encoding.UTF8.GetBytes("restored")),
            restoreTimeUtc,
            CancellationToken.None);

        Assert.Equal("restored", await File.ReadAllTextAsync(target));
        AssertWithinTwoSeconds(restoreTimeUtc.UtcDateTime, File.GetLastWriteTimeUtc(target));
        Assert.Empty(Directory.EnumerateFiles(root, "target.txt.winledger-*"));
    }

    [Fact]
    public async Task RestoreFileContentAsyncCreatesMissingParentDirectory()
    {
        var root = CreateTempDirectory();
        var target = Path.Combine(root, "nested", "target.txt");
        var provider = new WindowsFileSystemMutationProvider();

        await provider.RestoreFileContentAsync(
            root,
            target,
            Convert.ToBase64String(Encoding.UTF8.GetBytes("restored")),
            null,
            CancellationToken.None);

        Assert.Equal("restored", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(target)!, "target.txt.winledger-*"));
    }

    [Fact]
    public async Task RestoreFileContentAsyncRejectsTargetsOutsideRoot()
    {
        var root = CreateTempDirectory();
        var outsideTarget = Path.Combine(Path.GetTempPath(), $"winledger-outside-{Guid.NewGuid():N}.txt");
        var provider = new WindowsFileSystemMutationProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.RestoreFileContentAsync(
                root,
                outsideTarget,
                Convert.ToBase64String([]),
                null,
                CancellationToken.None));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WinLedgerFileSystemMutationProviderTests", Guid.NewGuid().ToString("N"));
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
