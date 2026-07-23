using WinLedger.Core.Abstractions;
using WinLedger.Windows.Hosts;

namespace WinLedger.Tests;

public sealed class WindowsHostsFileMutationProviderTests
{
    [Fact]
    public async Task ReadSnapshotAsyncRejectsOutsideHostsFilePath()
    {
        var provider = new WindowsHostsFileMutationProvider(new SystemClock());
        var outsidePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.hosts");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ReadSnapshotAsync(outsidePath, CancellationToken.None));
    }

    [Fact]
    public async Task RestoreContentAsyncRejectsOutsideHostsFilePath()
    {
        var provider = new WindowsHostsFileMutationProvider(new SystemClock());
        var outsidePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.hosts");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.RestoreContentAsync(outsidePath, Convert.ToBase64String([]), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteFileAsyncRejectsOutsideHostsFilePath()
    {
        var provider = new WindowsHostsFileMutationProvider(new SystemClock());
        var outsidePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.hosts");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.DeleteFileAsync(outsidePath, CancellationToken.None));
    }
}
