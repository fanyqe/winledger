using WinLedger.Domain.Rollback;
using WinLedger.Domain.Startup;

namespace WinLedger.Comparison.Startup;

public static class StartupChangeExplainer
{
    public static string Summarize(
        StartupEntryChangeKind kind,
        StartupEntrySnapshot? before,
        StartupEntrySnapshot? after)
    {
        var entry = after ?? before;
        var name = entry?.Name ?? "unknown startup entry";
        var source = entry?.Source.ToString() ?? "Unknown";

        return kind switch
        {
            StartupEntryChangeKind.EntryCreated => $"The startup entry \"{name}\" was added through {source}.",
            StartupEntryChangeKind.EntryRemoved => $"The startup entry \"{name}\" was removed from {source}.",
            StartupEntryChangeKind.CommandChanged => $"The startup entry \"{name}\" command changed from {Display(before?.Command)} to {Display(after?.Command)}.",
            StartupEntryChangeKind.EnabledChanged => $"The startup entry \"{name}\" enabled state changed from {before?.Enabled} to {after?.Enabled}.",
            StartupEntryChangeKind.MetadataChanged => $"The startup entry \"{name}\" metadata changed.",
            _ => $"The startup entry \"{name}\" changed."
        };
    }

    public static IReadOnlySet<ChangeAttentionLabel> Classify(
        StartupEntryChangeKind kind,
        StartupEntrySnapshot? before,
        StartupEntrySnapshot? after,
        RollbackAvailability rollbackAvailability)
    {
        var entry = after ?? before;
        var labels = new HashSet<ChangeAttentionLabel>
        {
            ChangeAttentionLabel.Persistent,
            ChangeAttentionLabel.StartupRelated
        };

        if (IsPrivileged(entry))
        {
            labels.Add(ChangeAttentionLabel.Privileged);
            labels.Add(ChangeAttentionLabel.SecuritySensitive);
        }

        if (kind is StartupEntryChangeKind.EntryRemoved or StartupEntryChangeKind.CommandChanged)
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
        StartupEntryChangeKind kind,
        StartupEntrySnapshot? after)
    {
        if (kind == StartupEntryChangeKind.EntryCreated &&
            after?.Source == StartupEntrySourceKind.StartupFolder)
        {
            return RollbackAvailability.RequiresConfirmation;
        }

        return kind == StartupEntryChangeKind.EntryRemoved
            ? RollbackAvailability.Unavailable
            : RollbackAvailability.ManualReview;
    }

    private static bool IsPrivileged(StartupEntrySnapshot? entry)
    {
        if (entry is null)
        {
            return false;
        }

        return entry.Source == StartupEntrySourceKind.WindowsService ||
               entry.Location.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(entry.RunAsUser, "SYSTEM", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(entry.RunAsUser, "LOCAL SYSTEM", StringComparison.OrdinalIgnoreCase);
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(empty)" : $"\"{value}\"";
    }
}
