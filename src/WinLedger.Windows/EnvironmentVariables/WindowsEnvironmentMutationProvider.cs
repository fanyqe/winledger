using WinLedger.Core.EnvironmentVariables;
using WinLedger.Domain.EnvironmentVariables;

namespace WinLedger.Windows.EnvironmentVariables;

public sealed class WindowsEnvironmentMutationProvider : IEnvironmentMutationProvider
{
    public Task<EnvironmentVariableSnapshot?> ReadVariableAsync(
        EnvironmentVariableScopeKind scope,
        string name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(EnvironmentVariableRegistry.ReadVariable(scope, name));
    }

    public Task SetVariableAsync(
        EnvironmentVariableSnapshot variable,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnvironmentVariableRegistry.SetVariable(variable);
        return Task.CompletedTask;
    }

    public Task DeleteVariableAsync(
        EnvironmentVariableScopeKind scope,
        string name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnvironmentVariableRegistry.DeleteVariable(scope, name);
        return Task.CompletedTask;
    }
}
