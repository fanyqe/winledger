using WinLedger.Domain.Registry;

namespace WinLedger.Core.Registry;

public interface IRegistryMutationProvider
{
    Task<RegistryValueSnapshot?> ReadValueAsync(
        RegistryPath keyPath,
        string valueName,
        CancellationToken cancellationToken);

    Task SetValueAsync(
        RegistryPath keyPath,
        RegistryValueSnapshot value,
        CancellationToken cancellationToken);

    Task DeleteValueAsync(
        RegistryPath keyPath,
        string valueName,
        CancellationToken cancellationToken);
}
