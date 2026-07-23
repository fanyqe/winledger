using System.Text.Json;
using WinLedger.Core.Reports;
using WinLedger.Domain;
using WinLedger.Domain.Hosts;
using WinLedger.Domain.Rollback;

namespace WinLedger.Core.Hosts;

public sealed class HostsFileReportExporter
{
    public string ExportJson(HostsFileComparison comparison, HostsFileRollbackPlan? rollbackPlan = null)
    {
        var report = new HostsFileReport(
            "1.0",
            comparison.SessionId,
            comparison.BaselineSnapshotId,
            comparison.ComparisonSnapshotId,
            comparison.ComparedAt,
            comparison.Changes,
            rollbackPlan?.Operations ?? Array.Empty<HostsFileRollbackOperation>(),
            comparison.Warnings.Concat(rollbackPlan?.Warnings ?? Array.Empty<string>()).ToArray());

        return JsonSerializer.Serialize(report, WinLedgerJsonSerializer.Options);
    }

    public string ExportHtml(HostsFileComparison comparison, HostsFileRollbackPlan? rollbackPlan = null)
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
          <title>WinLedger Hosts File Report</title>
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
          <h1>WinLedger Hosts File Report</h1>
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

    public string ExportText(HostsFileComparison comparison, HostsFileRollbackPlan? rollbackPlan = null)
    {
        return PlainTextReportFormatter.Format(
            "WinLedger Hosts File Report",
            comparison.ComparedAt,
            comparison.Changes.Select(change => new PlainTextReportChange(
                change.Kind.ToString(),
                change.TargetDisplayName,
                change.Summary,
                change.RollbackAvailability.ToString())).ToArray(),
            (rollbackPlan?.Operations ?? Array.Empty<HostsFileRollbackOperation>())
                .Select(operation => new PlainTextRollbackOperation(
                    operation.Kind.ToString(),
                    operation.TargetDisplayName,
                    operation.RequiresAdministrator,
                    operation.RequiresRestart)).ToArray(),
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

    private sealed record HostsFileReport(
        string SchemaVersion,
        Guid SessionId,
        Guid BaselineSnapshotId,
        Guid ComparisonSnapshotId,
        DateTimeOffset ComparedAt,
        IReadOnlyList<HostsFileChange> Changes,
        IReadOnlyList<HostsFileRollbackOperation> RollbackPlan,
        IReadOnlyList<string> Warnings);
}
