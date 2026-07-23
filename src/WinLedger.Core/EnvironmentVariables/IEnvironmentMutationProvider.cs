using WinLedger.Domain.EnvironmentVariables;

namespace WinLedger.Core.EnvironmentVariables;

public interface IEnvironmentMutationProvider
{
    Task<EnvironmentVariableSnapshot?> ReadVariableAsync(
        EnvironmentVariableScopeKind scope,
        string name,
        CancellationToken cancellationToken);

    Task SetVariableAsync(
        EnvironmentVariableSnapshot variable,
        CancellationToken cancellationToken);

    Task DeleteVariableAsync(
        EnvironmentVariableScopeKind scope,
        string name,
        CancellationToken cancellationToken);
}
