using WinLedger.Domain.Registry;

namespace WinLedger.Domain.Rollback;

public sealed record RegistryRollbackOperation(
    Guid Id,
    Guid ChangeId,
    RollbackOperationKind Kind,
    RegistryPath KeyPath,
    string ValueName,
    RegistryValueSnapshot? ExpectedCurrentValue,
    RegistryValueSnapshot? RestoreValue,
    bool RequiresAdministrator,
    bool RequiresRestart,
    RollbackValidationState ValidationState = RollbackValidationState.NotValidated,
    string? ValidationMessage = null)
{
    public string TargetDisplayName => $"{KeyPath}\\{(string.IsNullOrEmpty(ValueName) ? "(Default)" : ValueName)}";
}
