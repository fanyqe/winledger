namespace WinLedger.Domain.Registry;

public sealed record RegistryValueSnapshot(
    string Name,
    RegistryValueType ValueType,
    string SerializedValue,
    string DisplayValue)
{
    public string DisplayName => string.IsNullOrEmpty(Name) ? "(Default)" : Name;
}
