using System.Text.Json;
using WinLedger.Comparison.Hosts;
using WinLedger.Core.Hosts;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.Hosts;

namespace WinLedger.Tests;

public sealed class HostsFileReportExporterTests
{
    [Fact]
    public void ExportJsonIncludesVersionedHostsFileChangesAndRollbackPlan()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new HostsFileSnapshotComparer().Compare(
            HostsFileTestData.Snapshot(sessionId, "Before", "127.0.0.1 localhost\r\n"),
            HostsFileTestData.Snapshot(sessionId, "After", "127.0.0.1 localhost\r\n10.0.0.2 new.example\r\n"),
            DateTimeOffset.UtcNow);
        var plan = new HostsFileRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var json = new HostsFileReportExporter().ExportJson(comparison, plan);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("changes").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("rollbackPlan").GetArrayLength());
        Assert.Equal(nameof(HostsFileRollbackOperationKind.RestoreHostsFileContent), document.RootElement.GetProperty("rollbackPlan")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public void ExportHtmlEscapesHostsFileLineText()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new HostsFileSnapshotComparer().Compare(
            HostsFileTestData.Snapshot(sessionId, "Before", "127.0.0.1 localhost\r\n"),
            HostsFileTestData.Snapshot(sessionId, "After", "127.0.0.1 localhost\r\n<script> bad.example\r\n"),
            DateTimeOffset.UtcNow);

        var html = new HostsFileReportExporter().ExportHtml(comparison);

        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
    }
}
