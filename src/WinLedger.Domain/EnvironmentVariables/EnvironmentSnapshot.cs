namespace WinLedger.Domain.EnvironmentVariables;

public sealed record EnvironmentSnapshot(
    Guid Id,
    Guid SessionId,
    string Name,
    DateTimeOffset CapturedAt,
    IReadOnlyList<EnvironmentVariableSnapshot> Variables,
    IReadOnlyList<string> Warnings)
{
    public static EnvironmentSnapshot Empty(Guid sessionId, string name, DateTimeOffset capturedAt)
    {
        return new EnvironmentSnapshot(
            Guid.NewGuid(),
            sessionId,
            name,
            capturedAt,
            Array.Empty<EnvironmentVariableSnapshot>(),
            Array.Empty<string>());
    }
}
