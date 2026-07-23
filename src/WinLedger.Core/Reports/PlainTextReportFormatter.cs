using System.Text;

namespace WinLedger.Core.Reports;

public static class PlainTextReportFormatter
{
    public static string Format(
        string title,
        DateTimeOffset comparedAt,
        IReadOnlyList<PlainTextReportChange> changes,
        IReadOnlyList<PlainTextRollbackOperation> rollbackOperations,
        IEnumerable<string> warnings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(rollbackOperations);
        ArgumentNullException.ThrowIfNull(warnings);

        var warningList = warnings.Distinct(StringComparer.Ordinal).ToArray();
        var builder = new StringBuilder();

        builder.AppendLine(title);
        builder.AppendLine(new string('=', title.Length));
        builder.AppendLine();
        builder.AppendLine($"Compared at: {comparedAt:u}");
        builder.AppendLine($"Changes: {changes.Count}");
        builder.AppendLine($"Rollback operations: {rollbackOperations.Count}");
        builder.AppendLine($"Warnings: {warningList.Length}");
        builder.AppendLine();

        builder.AppendLine("Changes");
        builder.AppendLine("-------");
        if (changes.Count == 0)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            foreach (var change in changes)
            {
                builder.AppendLine($"- {change.Kind}: {change.Target}");
                builder.AppendLine($"  Summary: {change.Summary}");
                builder.AppendLine($"  Rollback: {change.RollbackAvailability}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Rollback Plan");
        builder.AppendLine("-------------");
        if (rollbackOperations.Count == 0)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            foreach (var operation in rollbackOperations)
            {
                builder.AppendLine($"- {operation.Kind}: {operation.Target}");
                builder.AppendLine($"  Requires administrator: {FormatBoolean(operation.RequiresAdministrator)}");
                builder.AppendLine($"  Requires restart: {FormatBoolean(operation.RequiresRestart)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Warnings");
        builder.AppendLine("--------");
        if (warningList.Length == 0)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            foreach (var warning in warningList)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        return builder.ToString();
    }

    private static string FormatBoolean(bool value)
    {
        return value ? "Yes" : "No";
    }
}

public sealed record PlainTextReportChange(
    string Kind,
    string Target,
    string Summary,
    string RollbackAvailability);

public sealed record PlainTextRollbackOperation(
    string Kind,
    string Target,
    bool RequiresAdministrator,
    bool RequiresRestart);
