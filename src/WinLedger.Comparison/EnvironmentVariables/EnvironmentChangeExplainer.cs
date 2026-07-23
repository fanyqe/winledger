using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Domain.Rollback;

namespace WinLedger.Comparison.EnvironmentVariables;

public static class EnvironmentChangeExplainer
{
    public static string Summarize(
        EnvironmentVariableChangeKind kind,
        EnvironmentVariableScopeKind scope,
        string name,
        EnvironmentVariableSnapshot? before,
        EnvironmentVariableSnapshot? after,
        string? pathEntry,
        int? beforeIndex,
        int? afterIndex)
    {
        return kind switch
        {
            EnvironmentVariableChangeKind.VariableCreated => $"The {scope} environment variable \"{name}\" was created.",
            EnvironmentVariableChangeKind.VariableRemoved => $"The {scope} environment variable \"{name}\" was removed.",
            EnvironmentVariableChangeKind.ValueChanged => $"The {scope} environment variable \"{name}\" changed from {DisplayValue(name, before?.RawValue)} to {DisplayValue(name, after?.RawValue)}.",
            EnvironmentVariableChangeKind.PathEntryAdded => $"The path entry {Display(pathEntry)} was added to the {scope} PATH.",
            EnvironmentVariableChangeKind.PathEntryRemoved => $"The path entry {Display(pathEntry)} was removed from the {scope} PATH.",
            EnvironmentVariableChangeKind.PathEntryReordered => $"The path entry {Display(pathEntry)} moved in the {scope} PATH from position {DisplayIndex(beforeIndex)} to {DisplayIndex(afterIndex)}.",
            _ => $"The {scope} environment variable \"{name}\" changed."
        };
    }

    public static IReadOnlySet<ChangeAttentionLabel> Classify(
        EnvironmentVariableChangeKind kind,
        EnvironmentVariableScopeKind scope,
        string name,
        RollbackAvailability rollbackAvailability)
    {
        var labels = new HashSet<ChangeAttentionLabel>
        {
            ChangeAttentionLabel.Persistent
        };

        if (scope == EnvironmentVariableScopeKind.Machine)
        {
            labels.Add(ChangeAttentionLabel.Privileged);
        }

        if (string.Equals(name, "Path", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add(ChangeAttentionLabel.SecuritySensitive);
        }

        if (kind is EnvironmentVariableChangeKind.VariableRemoved or EnvironmentVariableChangeKind.ValueChanged or
            EnvironmentVariableChangeKind.PathEntryRemoved or EnvironmentVariableChangeKind.PathEntryReordered)
        {
            labels.Add(ChangeAttentionLabel.PotentiallyDestructive);
        }

        if (rollbackAvailability is RollbackAvailability.Unavailable or RollbackAvailability.ManualReview)
        {
            labels.Add(ChangeAttentionLabel.RollbackUnavailable);
        }

        return labels;
    }

    public static RollbackAvailability GetRollbackAvailability(
        EnvironmentVariableChangeKind kind,
        EnvironmentVariableSnapshot? before,
        EnvironmentVariableSnapshot? after)
    {
        return kind switch
        {
            EnvironmentVariableChangeKind.VariableCreated when after is not null => RollbackAvailability.RequiresConfirmation,
            EnvironmentVariableChangeKind.VariableRemoved when CanRestore(before) => RollbackAvailability.RequiresConfirmation,
            EnvironmentVariableChangeKind.ValueChanged or
                EnvironmentVariableChangeKind.PathEntryAdded or
                EnvironmentVariableChangeKind.PathEntryRemoved or
                EnvironmentVariableChangeKind.PathEntryReordered
                when CanRestore(before) && after is not null => RollbackAvailability.RequiresConfirmation,
            _ => RollbackAvailability.ManualReview
        };
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(empty)" : $"\"{value}\"";
    }

    private static string DisplayValue(string name, string? value)
    {
        return IsSensitiveName(name) && !string.IsNullOrEmpty(value)
            ? "(redacted)"
            : Display(value);
    }

    private static string DisplayIndex(int? index)
    {
        return index.HasValue ? (index.Value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : "unknown";
    }

    private static bool IsSensitiveName(string name)
    {
        var sensitiveMarkers = new[]
        {
            "PASSWORD",
            "PASSWD",
            "TOKEN",
            "SECRET",
            "KEY",
            "PRIVATE",
            "CREDENTIAL",
            "CONNECTION_STRING",
            "API_KEY",
            "ACCESS_KEY",
            "AUTH",
            "CERT"
        };

        return sensitiveMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool CanRestore(EnvironmentVariableSnapshot? variable)
    {
        return variable?.ValueType is EnvironmentVariableValueType.String or EnvironmentVariableValueType.ExpandString;
    }
}
