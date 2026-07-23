namespace WinLedger.Domain.Registry;

public sealed record RegistrySnapshot(
    Guid Id,
    Guid SessionId,
    string Name,
    DateTimeOffset CapturedAt,
    IReadOnlyList<RegistrySnapshotTarget> Targets,
    IReadOnlyList<RegistryKeySnapshot> Keys,
    IReadOnlyList<string> Warnings)
{
    public static RegistrySnapshot Empty(Guid sessionId, string name, DateTimeOffset capturedAt)
    {
        return new RegistrySnapshot(
            Guid.NewGuid(),
            sessionId,
            name,
            capturedAt,
            Array.Empty<RegistrySnapshotTarget>(),
            Array.Empty<RegistryKeySnapshot>(),
            Array.Empty<string>());
    }
}
