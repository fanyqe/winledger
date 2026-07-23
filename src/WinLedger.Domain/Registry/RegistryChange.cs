using WinLedger.Domain.Rollback;

namespace WinLedger.Domain.Registry;

public sealed record RegistryChange(
    Guid Id,
    RegistryChangeKind Kind,
    RegistryPath KeyPath,
    string? ValueName,
    RegistryValueSnapshot? Before,
    RegistryValueSnapshot? After,
    string Summary,
    IReadOnlySet<ChangeAttentionLabel> Labels,
    RollbackAvailability RollbackAvailability)
{
    public string TargetDisplayName => ValueName is null
        ? KeyPath.ToString()
        : $"{KeyPath}\\{(string.IsNullOrEmpty(ValueName) ? "(Default)" : ValueName)}";
}
