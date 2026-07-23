namespace WinLedger.Domain.Registry;

public sealed record RegistryKeySnapshot(
    RegistryPath Path,
    IReadOnlyList<RegistryValueSnapshot> Values)
{
    public RegistryValueSnapshot? FindValue(string name)
    {
        return Values.FirstOrDefault(value =>
            string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
