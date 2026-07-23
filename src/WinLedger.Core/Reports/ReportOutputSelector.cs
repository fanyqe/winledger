using System.Text;

namespace WinLedger.Core.Reports;

public static class ReportOutputSelector
{
    public static string CreateReport(
        string outputPath,
        Func<string> jsonExporter,
        Func<string> htmlExporter,
        Func<string> textExporter,
        Func<string>? registryEditorExporter = null,
        Func<string>? powerShellExporter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(jsonExporter);
        ArgumentNullException.ThrowIfNull(htmlExporter);
        ArgumentNullException.ThrowIfNull(textExporter);

        if (UsesRegistryEditorExtension(outputPath))
        {
            if (registryEditorExporter is null)
            {
                throw new InvalidOperationException("The .reg export format is only available for registry reports.");
            }

            return registryEditorExporter!();
        }

        if (UsesPowerShellExtension(outputPath))
        {
            if (powerShellExporter is null)
            {
                throw new InvalidOperationException("The .ps1 export format is only available for reports with rollback script support.");
            }

            return powerShellExporter!();
        }

        if (UsesHtml(outputPath))
        {
            return htmlExporter();
        }

        return UsesText(outputPath) ? textExporter() : jsonExporter();
    }

    public static string FormatName(
        string outputPath,
        bool registryEditorSupported = false,
        bool powerShellSupported = false)
    {
        if (UsesRegistryEditorExtension(outputPath) && registryEditorSupported)
        {
            return "REG";
        }

        if (UsesPowerShellExtension(outputPath) && powerShellSupported)
        {
            return "POWERSHELL";
        }

        if (UsesHtml(outputPath))
        {
            return "HTML";
        }

        return UsesText(outputPath) ? "TEXT" : "JSON";
    }

    public static Encoding GetEncoding(string outputPath, bool registryEditorSupported = false)
    {
        if (UsesRegistryEditorExtension(outputPath))
        {
            return Encoding.Unicode;
        }

        return UsesPowerShellExtension(outputPath) ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true) : Encoding.UTF8;
    }

    public static bool UsesHtml(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var extension = Path.GetExtension(outputPath);
        return string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase);
    }

    public static bool UsesText(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var extension = Path.GetExtension(outputPath);
        return string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".text", StringComparison.OrdinalIgnoreCase);
    }

    public static bool UsesRegistryEditor(string outputPath, bool registryEditorSupported)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        return registryEditorSupported && UsesRegistryEditorExtension(outputPath);
    }

    public static bool UsesPowerShell(string outputPath, bool powerShellSupported)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        return powerShellSupported && UsesPowerShellExtension(outputPath);
    }

    private static bool UsesRegistryEditorExtension(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        return string.Equals(Path.GetExtension(outputPath), ".reg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesPowerShellExtension(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        return string.Equals(Path.GetExtension(outputPath), ".ps1", StringComparison.OrdinalIgnoreCase);
    }
}
