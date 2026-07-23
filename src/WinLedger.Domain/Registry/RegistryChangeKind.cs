namespace WinLedger.Domain.Registry;

public enum RegistryChangeKind
{
    KeyCreated,
    KeyRemoved,
    ValueCreated,
    ValueRemoved,
    ValueModified,
    ValueTypeChanged
}
