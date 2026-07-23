using WinLedger.Domain.Rollback;

namespace WinLedger.Domain.InstalledApplications;

public sealed record InstalledApplicationChange(
    Guid Id,
    InstalledApplicationChangeKind Kind,
    string ApplicationName,
    InstalledApplicationSnapshot? Before,
    InstalledApplicationSnapshot? After,
    string Summary,
    IReadOnlySet<ChangeAttentionLabel> Labels,
    RollbackAvailability RollbackAvailability)
{
    public string TargetDisplayName => ApplicationName;

    public InstalledApplicationSourceKind Source => (After ?? Before)?.Source ?? InstalledApplicationSourceKind.RegistryUninstall;
}
