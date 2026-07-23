namespace WinLedger.Domain.Startup;

public sealed record StartupSnapshot(
    Guid Id,
    Guid SessionId,
    string Name,
    DateTimeOffset CapturedAt,
    IReadOnlyList<StartupEntrySnapshot> Entries,
    IReadOnlyList<string> Warnings)
{
    public static StartupSnapshot Empty(Guid sessionId, string name, DateTimeOffset capturedAt)
    {
        return new StartupSnapshot(
            Guid.NewGuid(),
            sessionId,
            name,
            capturedAt,
            Array.Empty<StartupEntrySnapshot>(),
            Array.Empty<string>());
    }
}
