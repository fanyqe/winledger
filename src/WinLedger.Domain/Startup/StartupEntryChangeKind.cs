namespace WinLedger.Domain.Startup;

public enum StartupEntryChangeKind
{
    EntryCreated,
    EntryRemoved,
    CommandChanged,
    EnabledChanged,
    MetadataChanged
}
