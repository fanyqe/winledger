using WinLedger.Domain.Startup;

namespace WinLedger.Core.Startup;

public interface IStartupMutationProvider
{
    Task<StartupEntrySnapshot?> ReadStartupFolderEntryAsync(
        string filePath,
        CancellationToken cancellationToken);

    Task DeleteStartupFolderEntryAsync(
        string filePath,
        CancellationToken cancellationToken);
}
