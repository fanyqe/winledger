using System.Text;

namespace WinLedger.Core.Reports;

public static class PowerShellRollbackScriptExporter
{
    public static string Export(
        string title,
        string rollbackCommand,
        string reportJson,
        IReadOnlyList<PowerShellRollbackOperationSummary> operations,
        IEnumerable<string> warnings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(rollbackCommand);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportJson);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(warnings);

        var scriptId = Guid.NewGuid().ToString("N");
        var reportJsonBase64 = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(reportJson),
            Base64FormattingOptions.InsertLineBreaks);
        var builder = new StringBuilder();

        builder.AppendLine("#requires -Version 5.1");
        builder.AppendLine("<#");
        builder.AppendLine($"WinLedger rollback script: {SanitizeComment(title)}");
        builder.AppendLine("This script embeds a structured WinLedger JSON rollback report and calls WinLedger CLI.");
        builder.AppendLine("WinLedger CLI performs the expected-current-state validation before applying changes.");
        builder.AppendLine("#>");
        builder.AppendLine("[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]");
        builder.AppendLine("param(");
        builder.AppendLine("    [string]$WinLedgerCliPath = '',");
        builder.AppendLine("    [string[]]$OperationId = @('all')");
        builder.AppendLine(")");
        builder.AppendLine();
        builder.AppendLine("Set-StrictMode -Version Latest");
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine();
        AppendWarnings(builder, warnings);
        AppendOperations(builder, operations);
        builder.AppendLine("$ReportJsonBase64 = @'");
        builder.AppendLine(reportJsonBase64);
        builder.AppendLine("'@");
        builder.AppendLine();
        builder.AppendLine("$ReportJson = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($ReportJsonBase64))");
        builder.AppendLine($"$ReportPath = Join-Path ([System.IO.Path]::GetTempPath()) 'winledger-rollback-{scriptId}.json'");
        builder.AppendLine();
        builder.AppendLine("function Resolve-WinLedgerCliPath {");
        builder.AppendLine("    param([string]$CandidatePath)");
        builder.AppendLine();
        builder.AppendLine("    if (-not [string]::IsNullOrWhiteSpace($CandidatePath)) {");
        builder.AppendLine("        if (Test-Path -LiteralPath $CandidatePath) {");
        builder.AppendLine("            return $CandidatePath");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        throw \"WinLedger CLI was not found: $CandidatePath\"");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    $candidatePaths = @(");
        builder.AppendLine("        (Join-Path $PSScriptRoot 'cli\\WinLedger.Cli.exe'),");
        builder.AppendLine("        (Join-Path $PSScriptRoot 'WinLedger.Cli.exe')");
        builder.AppendLine("    )");
        builder.AppendLine();
        builder.AppendLine("    foreach ($path in $candidatePaths) {");
        builder.AppendLine("        if (Test-Path -LiteralPath $path) {");
        builder.AppendLine("            return $path");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    throw 'WinLedger CLI was not found. Pass -WinLedgerCliPath with the full path to WinLedger.Cli.exe.'");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("function Get-OperationSelection {");
        builder.AppendLine("    param([string[]]$RequestedOperationId)");
        builder.AppendLine();
        builder.AppendLine("    if ($RequestedOperationId.Count -eq 0) {");
        builder.AppendLine("        return 'all'");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    if ($RequestedOperationId.Count -eq 1 -and $RequestedOperationId[0].Equals('all', [System.StringComparison]::OrdinalIgnoreCase)) {");
        builder.AppendLine("        return 'all'");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    return [string]::Join(',', $RequestedOperationId)");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("try {");
        builder.AppendLine("    $resolvedCliPath = Resolve-WinLedgerCliPath -CandidatePath $WinLedgerCliPath");
        builder.AppendLine("    Set-Content -LiteralPath $ReportPath -Value $ReportJson -Encoding UTF8");
        builder.AppendLine("    $selection = Get-OperationSelection -RequestedOperationId $OperationId");
        builder.AppendLine($"    $command = '{EscapeSingleQuotedPowerShellString(rollbackCommand)}'");
        builder.AppendLine();
        builder.AppendLine("    if ($PSCmdlet.ShouldProcess(\"WinLedger rollback operation selection: $selection\", 'Apply rollback')) {");
        builder.AppendLine("        & $resolvedCliPath $command $ReportPath $selection");
        builder.AppendLine("        if ($LASTEXITCODE -ne 0) {");
        builder.AppendLine("            throw \"WinLedger CLI exited with code $LASTEXITCODE.\"");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("finally {");
        builder.AppendLine("    if (Test-Path -LiteralPath $ReportPath) {");
        builder.AppendLine("        Remove-Item -LiteralPath $ReportPath -Force -ErrorAction SilentlyContinue");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void AppendWarnings(StringBuilder builder, IEnumerable<string> warnings)
    {
        var warningList = warnings.Distinct(StringComparer.Ordinal).ToArray();
        if (warningList.Length == 0)
        {
            return;
        }

        builder.AppendLine("# Warnings:");
        foreach (var warning in warningList)
        {
            builder.AppendLine($"# - {SanitizeComment(warning)}");
        }

        builder.AppendLine();
    }

    private static void AppendOperations(StringBuilder builder, IReadOnlyList<PowerShellRollbackOperationSummary> operations)
    {
        builder.AppendLine("# Available operations:");
        if (operations.Count == 0)
        {
            builder.AppendLine("# - none");
            builder.AppendLine();
            return;
        }

        foreach (var operation in operations)
        {
            builder.AppendLine($"# - {operation.Id}: {SanitizeComment(operation.Kind)} -> {SanitizeComment(operation.Target)}");
            builder.AppendLine($"#   Requires administrator: {FormatBoolean(operation.RequiresAdministrator)}; requires restart: {FormatBoolean(operation.RequiresRestart)}");
        }

        builder.AppendLine();
    }

    private static string FormatBoolean(bool value)
    {
        return value ? "yes" : "no";
    }

    private static string SanitizeComment(string value)
    {
        return value.ReplaceLineEndings(" ");
    }

    private static string EscapeSingleQuotedPowerShellString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}

public sealed record PowerShellRollbackOperationSummary(
    Guid Id,
    string Kind,
    string Target,
    bool RequiresAdministrator,
    bool RequiresRestart);
