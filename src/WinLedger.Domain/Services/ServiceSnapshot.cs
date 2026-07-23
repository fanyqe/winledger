namespace WinLedger.Domain.Services;

public sealed record ServiceSnapshot(
    Guid Id,
    Guid SessionId,
    string Name,
    DateTimeOffset CapturedAt,
    IReadOnlyList<WindowsServiceSnapshot> Services,
    IReadOnlyList<string> Warnings)
{
    public static ServiceSnapshot Empty(Guid sessionId, string name, DateTimeOffset capturedAt)
    {
        return new ServiceSnapshot(
            Guid.NewGuid(),
            sessionId,
            name,
            capturedAt,
            Array.Empty<WindowsServiceSnapshot>(),
            Array.Empty<string>());
    }
}
