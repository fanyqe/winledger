using WinLedger.Domain.Rollback;

namespace WinLedger.Domain.Services;

public sealed record ServiceChange(
    Guid Id,
    ServiceChangeKind Kind,
    string ServiceName,
    WindowsServiceSnapshot? Before,
    WindowsServiceSnapshot? After,
    string Summary,
    IReadOnlySet<ChangeAttentionLabel> Labels,
    RollbackAvailability RollbackAvailability)
{
    public string TargetDisplayName => ServiceName;
}
