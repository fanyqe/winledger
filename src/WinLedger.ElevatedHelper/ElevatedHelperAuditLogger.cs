namespace WinLedger.ElevatedHelper;

internal sealed class ElevatedHelperAuditLogger
{
    private readonly string logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLedger",
        "Logs",
        "elevated-helper.log");

    public async Task WriteAsync(Guid requestId, string message, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? ".");
        var line = $"{DateTimeOffset.UtcNow:O} request={requestId} {message}{Environment.NewLine}";
        await File.AppendAllTextAsync(logPath, line, cancellationToken).ConfigureAwait(false);
    }
}
