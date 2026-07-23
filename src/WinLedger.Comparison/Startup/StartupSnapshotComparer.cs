using WinLedger.Domain.Startup;

namespace WinLedger.Comparison.Startup;

public sealed class StartupSnapshotComparer
{
    public StartupComparison Compare(StartupSnapshot baseline, StartupSnapshot comparison, DateTimeOffset comparedAt)
    {
        if (baseline.SessionId != comparison.SessionId)
        {
            throw new ArgumentException("Startup snapshots belong to different sessions.", nameof(comparison));
        }

        var changes = new List<StartupChange>();
        var baselineEntries = baseline.Entries.ToDictionary(entry => entry.StableId, StringComparer.OrdinalIgnoreCase);
        var comparisonEntries = comparison.Entries.ToDictionary(entry => entry.StableId, StringComparer.OrdinalIgnoreCase);

        foreach (var (stableId, after) in comparisonEntries)
        {
            if (!baselineEntries.TryGetValue(stableId, out var before))
            {
                changes.Add(CreateChange(StartupEntryChangeKind.EntryCreated, stableId, null, after));
                continue;
            }

            AddEntryChanges(stableId, before, after, changes);
        }

        foreach (var (stableId, before) in baselineEntries)
        {
            if (!comparisonEntries.ContainsKey(stableId))
            {
                changes.Add(CreateChange(StartupEntryChangeKind.EntryRemoved, stableId, before, null));
            }
        }

        return new StartupComparison(
            Guid.NewGuid(),
            baseline.SessionId,
            baseline.Id,
            comparison.Id,
            comparedAt,
            changes.OrderBy(change => change.TargetDisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(change => change.Kind).ToArray(),
            baseline.Warnings.Concat(comparison.Warnings).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void AddEntryChanges(
        string stableId,
        StartupEntrySnapshot before,
        StartupEntrySnapshot after,
        List<StartupChange> changes)
    {
        if (!string.Equals(before.Command, after.Command, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(CreateChange(StartupEntryChangeKind.CommandChanged, stableId, before, after));
        }

        if (before.Enabled != after.Enabled)
        {
            changes.Add(CreateChange(StartupEntryChangeKind.EnabledChanged, stableId, before, after));
        }

        if (!MetadataMatches(before, after) &&
            !changes.Any(change => change.StableId.Equals(stableId, StringComparison.OrdinalIgnoreCase)))
        {
            changes.Add(CreateChange(StartupEntryChangeKind.MetadataChanged, stableId, before, after));
        }
    }

    private static StartupChange CreateChange(
        StartupEntryChangeKind kind,
        string stableId,
        StartupEntrySnapshot? before,
        StartupEntrySnapshot? after)
    {
        var availability = StartupChangeExplainer.GetRollbackAvailability(kind, after);

        return new StartupChange(
            Guid.NewGuid(),
            kind,
            stableId,
            before,
            after,
            StartupChangeExplainer.Summarize(kind, before, after),
            StartupChangeExplainer.Classify(kind, before, after, availability),
            availability);
    }

    private static bool MetadataMatches(StartupEntrySnapshot before, StartupEntrySnapshot after)
    {
        return before.Source == after.Source &&
               string.Equals(before.Name, after.Name, StringComparison.Ordinal) &&
               string.Equals(before.Location, after.Location, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(before.RunAsUser, after.RunAsUser, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(before.TriggerDescription, after.TriggerDescription, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(before.SourceSubsystem, after.SourceSubsystem, StringComparison.Ordinal) &&
               before.FileSize == after.FileSize &&
               before.LastWriteTimeUtc == after.LastWriteTimeUtc;
    }
}
