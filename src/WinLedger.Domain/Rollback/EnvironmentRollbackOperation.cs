using WinLedger.Domain.EnvironmentVariables;

namespace WinLedger.Domain.Rollback;

public sealed record EnvironmentRollbackOperation(
    Guid Id,
    Guid ChangeId,
    EnvironmentRollbackOperationKind Kind,
    EnvironmentVariableScopeKind Scope,
    string Name,
    EnvironmentVariableSnapshot? ExpectedCurrentVariable,
    EnvironmentVariableSnapshot? RestoreVariable,
    bool RequiresAdministrator,
    bool RequiresRestart)
{
    public string TargetDisplayName => $"{Scope} {Name}";
}
