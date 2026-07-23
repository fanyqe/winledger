using WinLedger.Domain.Rollback;

namespace WinLedger.Domain.EnvironmentVariables;

public sealed record EnvironmentVariableChange(
    Guid Id,
    EnvironmentVariableChangeKind Kind,
    EnvironmentVariableScopeKind Scope,
    string Name,
    EnvironmentVariableSnapshot? Before,
    EnvironmentVariableSnapshot? After,
    string? PathEntry,
    int? BeforeIndex,
    int? AfterIndex,
    string Summary,
    IReadOnlySet<ChangeAttentionLabel> Labels,
    RollbackAvailability RollbackAvailability)
{
    public string TargetDisplayName => PathEntry is null
        ? $"{Scope} {Name}"
        : $"{Scope} {Name}: {PathEntry}";
}
