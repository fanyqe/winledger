using System.Text.Json;
using WinLedger.Comparison.Firewall;
using WinLedger.Core.Firewall;
using WinLedger.Domain.Rollback;
using WinLedger.Rollback.Firewall;

namespace WinLedger.Tests;

public sealed class FirewallReportExporterTests
{
    [Fact]
    public void ExportJsonIncludesVersionedFirewallChangesAndRollbackPlan()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new FirewallSnapshotComparer().Compare(
            FirewallTestData.Snapshot(sessionId),
            FirewallTestData.Snapshot(sessionId, FirewallTestData.Rule("Created rule")),
            DateTimeOffset.UtcNow);
        var plan = new FirewallRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var json = new FirewallReportExporter().ExportJson(comparison, plan);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("changes").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("rollbackPlan").GetArrayLength());
        Assert.Equal(nameof(FirewallRollbackOperationKind.DeleteFirewallRule), document.RootElement.GetProperty("rollbackPlan")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public void ExportHtmlEscapesFirewallRuleNames()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new FirewallSnapshotComparer().Compare(
            FirewallTestData.Snapshot(sessionId),
            FirewallTestData.Snapshot(sessionId, FirewallTestData.Rule("<script>")),
            DateTimeOffset.UtcNow);

        var html = new FirewallReportExporter().ExportHtml(comparison);

        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
    }
}
