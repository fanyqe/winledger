namespace WinLedger.Domain.ScheduledTasks;

public sealed record ScheduledTaskSnapshot(
    Guid Id,
    Guid SessionId,
    string Name,
    DateTimeOffset CapturedAt,
    IReadOnlyList<ScheduledTaskDefinitionSnapshot> Tasks,
    IReadOnlyList<string> Warnings)
{
    public static ScheduledTaskSnapshot Empty(Guid sessionId, string name, DateTimeOffset capturedAt)
    {
        return new ScheduledTaskSnapshot(
            Guid.NewGuid(),
            sessionId,
            name,
            capturedAt,
            Array.Empty<ScheduledTaskDefinitionSnapshot>(),
            Array.Empty<string>());
    }
}
