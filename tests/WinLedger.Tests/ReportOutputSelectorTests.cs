using WinLedger.Core.Reports;

public sealed class ReportOutputSelectorTests
{
    [Theory]
    [InlineData("report.html")]
    [InlineData("report.htm")]
    [InlineData("REPORT.HTML")]
    public void CreateReportUsesHtmlExporterForHtmlExtensions(string outputPath)
    {
        var report = ReportOutputSelector.CreateReport(outputPath, () => "json", () => "html", () => "text");

        Assert.Equal("html", report);
        Assert.Equal("HTML", ReportOutputSelector.FormatName(outputPath));
    }

    [Theory]
    [InlineData("report.txt")]
    [InlineData("report.text")]
    [InlineData("REPORT.TXT")]
    public void CreateReportUsesTextExporterForTextExtensions(string outputPath)
    {
        var report = ReportOutputSelector.CreateReport(outputPath, () => "json", () => "html", () => "text");

        Assert.Equal("text", report);
        Assert.Equal("TEXT", ReportOutputSelector.FormatName(outputPath));
    }

    [Theory]
    [InlineData("report.json")]
    [InlineData("report")]
    [InlineData("report.log")]
    public void CreateReportUsesJsonExporterForOtherExtensions(string outputPath)
    {
        var report = ReportOutputSelector.CreateReport(outputPath, () => "json", () => "html", () => "text");

        Assert.Equal("json", report);
        Assert.Equal("JSON", ReportOutputSelector.FormatName(outputPath));
    }

    [Fact]
    public void CreateReportUsesRegistryEditorExporterForSupportedRegistryExtension()
    {
        var report = ReportOutputSelector.CreateReport(
            "report.reg",
            () => "json",
            () => "html",
            () => "text",
            () => "reg");

        Assert.Equal("reg", report);
        Assert.Equal("REG", ReportOutputSelector.FormatName("report.reg", registryEditorSupported: true));
        Assert.Equal(System.Text.Encoding.Unicode, ReportOutputSelector.GetEncoding("report.reg", registryEditorSupported: true));
    }

    [Fact]
    public void CreateReportUsesPowerShellExporterForSupportedPowerShellExtension()
    {
        var report = ReportOutputSelector.CreateReport(
            "report.ps1",
            () => "json",
            () => "html",
            () => "text",
            powerShellExporter: () => "powershell");

        Assert.Equal("powershell", report);
        Assert.Equal("POWERSHELL", ReportOutputSelector.FormatName("report.ps1", powerShellSupported: true));
        Assert.True(ReportOutputSelector.GetEncoding("report.ps1").GetPreamble().Length > 0);
    }

    [Fact]
    public void CreateReportRejectsRegistryEditorExtensionWhenUnsupported()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReportOutputSelector.CreateReport("report.reg", () => "json", () => "html", () => "text"));

        Assert.Contains("registry reports", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateReportRejectsPowerShellExtensionWhenUnsupported()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReportOutputSelector.CreateReport("report.ps1", () => "json", () => "html", () => "text"));

        Assert.Contains("rollback script support", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
