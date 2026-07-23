using System.Text.Json;
using WinLedger.Comparison.InstalledApplications;
using WinLedger.Core.InstalledApplications;
using WinLedger.Rollback.InstalledApplications;

namespace WinLedger.Tests;

public sealed class InstalledApplicationReportExporterTests
{
    [Fact]
    public void ExportJsonIncludesVersionedInstalledApplicationChangesAndManualReviewPlan()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new InstalledApplicationsSnapshotComparer().Compare(
            InstalledApplicationTestData.Snapshot(sessionId),
            InstalledApplicationTestData.Snapshot(sessionId, InstalledApplicationTestData.Application("Created App")),
            DateTimeOffset.UtcNow);
        var plan = new InstalledApplicationRollbackPlanner().CreatePlan(comparison, DateTimeOffset.UtcNow);

        var json = new InstalledApplicationReportExporter().ExportJson(comparison, plan);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("changes").GetArrayLength());
        Assert.Equal(0, document.RootElement.GetProperty("rollbackPlan").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("warnings").GetArrayLength());
    }

    [Fact]
    public void ExportHtmlEscapesInstalledApplicationNames()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new InstalledApplicationsSnapshotComparer().Compare(
            InstalledApplicationTestData.Snapshot(sessionId),
            InstalledApplicationTestData.Snapshot(sessionId, InstalledApplicationTestData.Application("<script>")),
            DateTimeOffset.UtcNow);

        var html = new InstalledApplicationReportExporter().ExportHtml(comparison);

        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportJsonIncludesAppxPackageMetadata()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new InstalledApplicationsSnapshotComparer().Compare(
            InstalledApplicationTestData.Snapshot(sessionId),
            InstalledApplicationTestData.Snapshot(sessionId, InstalledApplicationTestData.AppxPackage("Example Package")),
            DateTimeOffset.UtcNow);

        var json = new InstalledApplicationReportExporter().ExportJson(comparison);

        using var document = JsonDocument.Parse(json);
        var after = document.RootElement.GetProperty("changes")[0].GetProperty("after");
        Assert.Equal("AppxPackage", after.GetProperty("source").GetString());
        Assert.Equal("Example.Package_1.0.0.0_x64__publisherid", after.GetProperty("packageFullName").GetString());
        Assert.Equal("Example.Package_publisherid", after.GetProperty("packageFamilyName").GetString());
        Assert.Equal("publisherid", after.GetProperty("packagePublisherId").GetString());
    }

    [Fact]
    public void ExportHtmlShowsAppxPackageSource()
    {
        var sessionId = Guid.NewGuid();
        var comparison = new InstalledApplicationsSnapshotComparer().Compare(
            InstalledApplicationTestData.Snapshot(sessionId),
            InstalledApplicationTestData.Snapshot(sessionId, InstalledApplicationTestData.AppxPackage("Example Package")),
            DateTimeOffset.UtcNow);

        var html = new InstalledApplicationReportExporter().ExportHtml(comparison);

        Assert.Contains("AppxPackage", html, StringComparison.Ordinal);
        Assert.Contains("AppX/MSIX package", html, StringComparison.Ordinal);
    }
}
