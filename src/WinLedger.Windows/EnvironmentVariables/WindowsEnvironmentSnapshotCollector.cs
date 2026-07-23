using WinLedger.Core.Abstractions;
using WinLedger.Core.EnvironmentVariables;
using WinLedger.Domain.EnvironmentVariables;

namespace WinLedger.Windows.EnvironmentVariables;

public sealed class WindowsEnvironmentSnapshotCollector(IClock clock) : IEnvironmentSnapshotCollector
{
    public Task<EnvironmentSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var warnings = new List<string>();
        var variables = new List<EnvironmentVariableSnapshot>();
        variables.AddRange(EnvironmentVariableRegistry.ReadScope(EnvironmentVariableScopeKind.User, warnings));

        cancellationToken.ThrowIfCancellationRequested();
        variables.AddRange(EnvironmentVariableRegistry.ReadScope(EnvironmentVariableScopeKind.Machine, warnings));

        return Task.FromResult(new EnvironmentSnapshot(
            Guid.NewGuid(),
            sessionId,
            snapshotName,
            clock.UtcNow,
            variables.OrderBy(variable => variable.Scope).ThenBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            warnings.Distinct(StringComparer.Ordinal).ToArray()));
    }
}
