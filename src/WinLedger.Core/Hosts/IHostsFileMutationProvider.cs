using WinLedger.Domain.Hosts;

namespace WinLedger.Core.Hosts;

public interface IHostsFileMutationProvider
{
    Task<HostsFileSnapshot> ReadSnapshotAsync(
        string filePath,
        CancellationToken cancellationToken);

    Task RestoreContentAsync(
        string filePath,
        string contentBase64,
        CancellationToken cancellationToken);

    Task DeleteFileAsync(
        string filePath,
        CancellationToken cancellationToken);
}
