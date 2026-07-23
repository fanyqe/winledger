using System.Text.Json;
using WinLedger.Comparison.FileSystem;
using WinLedger.Core.FileSystem;
using WinLedger.Rollback.FileSystem;

namespace WinLedger.Tests;

public sealed class FileSystemReportExporterTests
{
    [Fact]
    public void ExportJsonIncludesVersionedFileSystemChangesAndRollbackPlan()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new FileSystemSnapshotComparer().Compare(
            FileSystemTestData.Snapshot(sessionId),
            FileSystemTestData.Snapshot(sessionId, FileSystemTestData.File("created.txt")),
            DateTimeOffset.UtcNow);
        var plan = new FileSystemRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var json = new FileSystemReportExporter().ExportJson(comparison, plan);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("changes").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("rollbackPlan").GetArrayLength());
    }

    [Fact]
    public void ExportHtmlEscapesFileSystemPaths()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new FileSystemSnapshotComparer().Compare(
            FileSystemTestData.Snapshot(sessionId),
            FileSystemTestData.Snapshot(sessionId, FileSystemTestData.File("<script>.txt")),
            DateTimeOffset.UtcNow);

        var html = new FileSystemReportExporter().ExportHtml(comparison);

        Assert.Contains("&lt;script&gt;.txt", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
    }
}
