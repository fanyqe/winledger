namespace WinLedger.Domain.Services;

public enum ServiceStartModeKind
{
    Unknown,
    Boot,
    System,
    Automatic,
    Manual,
    Disabled
}
