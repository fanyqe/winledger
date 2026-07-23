namespace WinLedger.Domain.InstalledApplications;

public sealed record InstalledApplicationsSnapshot(
    Guid Id,
    Guid SessionId,
    string Name,
    DateTimeOffset CapturedAt,
    IReadOnlyList<InstalledApplicationSnapshot> Applications,
    IReadOnlyList<string> Warnings)
{
    public static InstalledApplicationsSnapshot Empty(Guid sessionId, string name, DateTimeOffset capturedAt)
    {
        return new InstalledApplicationsSnapshot(
            Guid.NewGuid(),
            sessionId,
            name,
            capturedAt,
            Array.Empty<InstalledApplicationSnapshot>(),
            Array.Empty<string>());
    }
}
