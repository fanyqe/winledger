using WinLedger.Windows.Startup;

namespace WinLedger.Tests;

public sealed class WindowsStartupMutationProviderTests
{
    [Fact]
    public async Task ProviderRejectsPathsOutsideKnownStartupFolders()
    {
        var provider = new WindowsStartupMutationProvider();
        var outsidePath = Path.Combine(Path.GetTempPath(), "WinLedgerTests", $"{Guid.NewGuid():N}.lnk");

        var entry = await provider.ReadStartupFolderEntryAsync(outsidePath, CancellationToken.None);

        Assert.Null(entry);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.DeleteStartupFolderEntryAsync(outsidePath, CancellationToken.None));
    }
}
