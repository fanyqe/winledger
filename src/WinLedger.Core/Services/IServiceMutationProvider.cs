using WinLedger.Domain.Services;

namespace WinLedger.Core.Services;

public interface IServiceMutationProvider
{
    Task<WindowsServiceSnapshot?> ReadServiceAsync(
        string serviceName,
        CancellationToken cancellationToken);

    Task SetStartModeAsync(
        string serviceName,
        ServiceStartModeKind startMode,
        CancellationToken cancellationToken);

    Task SetDelayedAutoStartAsync(
        string serviceName,
        bool delayedAutoStart,
        CancellationToken cancellationToken);
}
