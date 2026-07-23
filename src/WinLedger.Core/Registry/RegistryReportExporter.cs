using System.Globalization;
using System.Text;
using System.Text.Json;
using WinLedger.Domain;
using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;
using WinLedger.Core.Reports;

namespace WinLedger.Core.Registry;

public sealed class RegistryReportExporter
{
    public string ExportJson(RegistryComparison comparison, RegistryRollbackPlan? rollbackPlan = null)
    {
        var report = new RegistryReport(
            "1.0",
            comparison.SessionId,
            comparison.BaselineSnapshotId,
            comparison.ComparisonSnapshotId,
            comparison.ComparedAt,
            comparison.Changes,
            rollbackPlan?.Operations ?? Array.Empty<RegistryRollbackOperation>(),
            comparison.Warnings.Concat(rollbackPlan?.Warnings ?? Array.Empty<string>()).ToArray());

        return JsonSerializer.Serialize(report, WinLedgerJsonSerializer.Options);
    }

    public string ExportHtml(RegistryComparison comparison, RegistryRollbackPlan? rollbackPlan = null)
    {
        var rows = comparison.Changes.Select(change =>
            $"""
            <tr>
              <td>{Escape(change.Kind.ToString())}</td>
              <td>{Escape(change.TargetDisplayName)}</td>
              <td>{Escape(change.Summary)}</td>
              <td>{Escape(change.RollbackAvailability.ToString())}</td>
            </tr>
            """);

        var operationCount = rollbackPlan?.Operations.Count ?? 0;

        return $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>WinLedger Registry Report</title>
          <style>
            body { font-family: Segoe UI, Arial, sans-serif; margin: 32px; color: #1f2937; }
            h1 { margin-bottom: 4px; }
            table { width: 100%; border-collapse: collapse; margin-top: 24px; }
            th, td { text-align: left; border-bottom: 1px solid #d1d5db; padding: 10px; vertical-align: top; }
            th { background: #f3f4f6; }
            .muted { color: #6b7280; }
          </style>
        </head>
        <body>
          <h1>WinLedger Registry Report</h1>
          <p class="muted">Compared at {{Escape(comparison.ComparedAt.ToString("u"))}}. Rollback operations: {{operationCount}}.</p>
          <table>
            <thead>
              <tr><th>Change</th><th>Target</th><th>Summary</th><th>Rollback</th></tr>
            </thead>
            <tbody>
              {{string.Join(Environment.NewLine, rows)}}
            </tbody>
          </table>
        </body>
        </html>
        """;
    }

    public string ExportText(RegistryComparison comparison, RegistryRollbackPlan? rollbackPlan = null)
    {
        return PlainTextReportFormatter.Format(
            "WinLedger Registry Report",
            comparison.ComparedAt,
            comparison.Changes.Select(change => new PlainTextReportChange(
                change.Kind.ToString(),
                change.TargetDisplayName,
                change.Summary,
                change.RollbackAvailability.ToString())).ToArray(),
            (rollbackPlan?.Operations ?? Array.Empty<RegistryRollbackOperation>())
                .Select(operation => new PlainTextRollbackOperation(
                    operation.Kind.ToString(),
                    operation.TargetDisplayName,
                    operation.RequiresAdministrator,
                    operation.RequiresRestart)).ToArray(),
            comparison.Warnings.Concat(rollbackPlan?.Warnings ?? Array.Empty<string>()));
    }

    public string ExportReg(RegistryComparison comparison, RegistryRollbackPlan? rollbackPlan = null)
    {
        var operations = rollbackPlan?.Operations ?? Array.Empty<RegistryRollbackOperation>();
        var warnings = comparison.Warnings.Concat(rollbackPlan?.Warnings ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var builder = new StringBuilder();

        builder.AppendLine("Windows Registry Editor Version 5.00");
        builder.AppendLine();
        builder.AppendLine($"; WinLedger registry rollback export");
        builder.AppendLine($"; Compared at: {comparison.ComparedAt:u}");
        builder.AppendLine($"; SessionId: {comparison.SessionId}");
        builder.AppendLine("; Review the JSON report before importing this file.");

        foreach (var warning in warnings)
        {
            builder.AppendLine($"; Warning: {EscapeRegComment(warning)}");
        }

        foreach (var operation in operations)
        {
            AppendRegOperation(builder, operation);
        }

        return builder.ToString();
    }

    public string ExportPowerShell(RegistryComparison comparison, RegistryRollbackPlan? rollbackPlan = null)
    {
        var planOperations = rollbackPlan?.Operations ?? Array.Empty<RegistryRollbackOperation>();
        var operationSummaries = planOperations
            .Select(operation => new PowerShellRollbackOperationSummary(
                operation.Id,
                operation.Kind.ToString(),
                operation.TargetDisplayName,
                operation.RequiresAdministrator,
                operation.RequiresRestart))
            .ToArray();

        return PowerShellRollbackScriptExporter.Export(
            "WinLedger Registry Rollback",
            "registry-rollback-apply",
            ExportJson(comparison, rollbackPlan),
            operationSummaries,
            comparison.Warnings.Concat(rollbackPlan?.Warnings ?? Array.Empty<string>()));
    }

    private static string Escape(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private static void AppendRegOperation(StringBuilder builder, RegistryRollbackOperation operation)
    {
        builder.AppendLine();
        builder.AppendLine($"; OperationId: {operation.Id}");
        builder.AppendLine($"; ChangeId: {operation.ChangeId}");
        builder.AppendLine($"; Kind: {operation.Kind}");
        if (operation.KeyPath.View != RegistryViewKind.Default)
        {
            builder.AppendLine($"; Registry view: {operation.KeyPath.View}. Import tools may use the process default registry view.");
        }

        builder.AppendLine($"[{operation.KeyPath.FullPath}]");

        switch (operation.Kind)
        {
            case RollbackOperationKind.DeleteRegistryValue:
                builder.AppendLine($"{FormatValueName(operation.ValueName)}=-");
                break;

            case RollbackOperationKind.SetRegistryValue when operation.RestoreValue is not null:
                if (TryFormatValueData(operation.RestoreValue, out var formattedValue))
                {
                    builder.AppendLine($"{FormatValueName(operation.ValueName)}={formattedValue}");
                }
                else
                {
                    builder.AppendLine($"; Skipped: {EscapeRegComment(operation.TargetDisplayName)} uses unsupported value type {operation.RestoreValue.ValueType}.");
                }

                break;

            case RollbackOperationKind.SetRegistryValue:
                builder.AppendLine($"; Skipped: {EscapeRegComment(operation.TargetDisplayName)} has no restore value.");
                break;
        }
    }

    private static string FormatValueName(string valueName)
    {
        return string.IsNullOrEmpty(valueName)
            ? "@"
            : $"\"{EscapeRegString(valueName)}\"";
    }

    private static bool TryFormatValueData(RegistryValueSnapshot value, out string formattedValue)
    {
        formattedValue = value.ValueType switch
        {
            RegistryValueType.String => $"\"{EscapeRegString(ReadJsonString(value.SerializedValue))}\"",
            RegistryValueType.ExpandString => $"hex(2):{FormatHex(EncodeNullTerminatedString(ReadJsonString(value.SerializedValue)))}",
            RegistryValueType.Binary => $"hex:{FormatHex(Convert.FromBase64String(ReadJsonString(value.SerializedValue)))}",
            RegistryValueType.DWord => $"dword:{ReadUInt32(value.SerializedValue).ToString("x8", CultureInfo.InvariantCulture)}",
            RegistryValueType.MultiString => $"hex(7):{FormatHex(EncodeMultiString(value.SerializedValue))}",
            RegistryValueType.QWord => $"hex(b):{FormatHex(BitConverter.GetBytes(ReadUInt64(value.SerializedValue)))}",
            RegistryValueType.None => "hex(0):",
            _ => string.Empty
        };

        return value.ValueType is not RegistryValueType.Unknown;
    }

    private static string ReadJsonString(string serializedValue)
    {
        return JsonSerializer.Deserialize<string>(serializedValue) ?? string.Empty;
    }

    private static uint ReadUInt32(string serializedValue)
    {
        return unchecked((uint)JsonSerializer.Deserialize<long>(serializedValue));
    }

    private static ulong ReadUInt64(string serializedValue)
    {
        return unchecked((ulong)JsonSerializer.Deserialize<long>(serializedValue));
    }

    private static byte[] EncodeNullTerminatedString(string value)
    {
        return Encoding.Unicode.GetBytes(value + '\0');
    }

    private static byte[] EncodeMultiString(string serializedValue)
    {
        var values = JsonSerializer.Deserialize<string[]>(serializedValue) ?? Array.Empty<string>();
        var joined = values.Length == 0
            ? "\0\0"
            : string.Join('\0', values) + "\0\0";
        return Encoding.Unicode.GetBytes(joined);
    }

    private static string FormatHex(byte[] bytes)
    {
        return string.Join(",", bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string EscapeRegString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapeRegComment(string value)
    {
        return value.ReplaceLineEndings(" ");
    }

    private sealed record RegistryReport(
        string SchemaVersion,
        Guid SessionId,
        Guid BaselineSnapshotId,
        Guid ComparisonSnapshotId,
        DateTimeOffset ComparedAt,
        IReadOnlyList<RegistryChange> Changes,
        IReadOnlyList<RegistryRollbackOperation> RollbackPlan,
        IReadOnlyList<string> Warnings);
}
