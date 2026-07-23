namespace WinLedger.Domain.Hosts;

public sealed record HostsFileSnapshot(
    Guid Id,
    Guid SessionId,
    string Name,
    DateTimeOffset CapturedAt,
    string FilePath,
    bool Exists,
    string? Content,
    string? ContentBase64,
    string? ContentSha256,
    long Length,
    DateTimeOffset? LastWriteTimeUtc,
    IReadOnlyList<HostsFileLineSnapshot> Lines,
    IReadOnlyList<string> Warnings)
{
    public static HostsFileSnapshot Missing(
        Guid sessionId,
        string name,
        DateTimeOffset capturedAt,
        string filePath,
        IReadOnlyList<string>? warnings = null)
    {
        return new HostsFileSnapshot(
            Guid.NewGuid(),
            sessionId,
            name,
            capturedAt,
            filePath,
            false,
            null,
            null,
            null,
            0,
            null,
            Array.Empty<HostsFileLineSnapshot>(),
            warnings ?? Array.Empty<string>());
    }
}
