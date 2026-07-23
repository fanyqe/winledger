using WinLedger.Domain.EnvironmentVariables;

namespace WinLedger.Comparison.EnvironmentVariables;

public sealed class EnvironmentSnapshotComparer
{
    public EnvironmentComparison Compare(EnvironmentSnapshot baseline, EnvironmentSnapshot comparison, DateTimeOffset comparedAt)
    {
        if (baseline.SessionId != comparison.SessionId)
        {
            throw new ArgumentException("Environment snapshots belong to different sessions.", nameof(comparison));
        }

        var changes = new List<EnvironmentVariableChange>();
        var baselineVariables = baseline.Variables.ToDictionary(VariableIdentity, StringComparer.OrdinalIgnoreCase);
        var comparisonVariables = comparison.Variables.ToDictionary(VariableIdentity, StringComparer.OrdinalIgnoreCase);

        foreach (var (identity, after) in comparisonVariables)
        {
            if (!baselineVariables.TryGetValue(identity, out var before))
            {
                changes.Add(CreateChange(EnvironmentVariableChangeKind.VariableCreated, after.Scope, after.Name, null, after, null, null, null));
                continue;
            }

            AddVariableChanges(before, after, changes);
        }

        foreach (var (identity, before) in baselineVariables)
        {
            if (!comparisonVariables.ContainsKey(identity))
            {
                changes.Add(CreateChange(EnvironmentVariableChangeKind.VariableRemoved, before.Scope, before.Name, before, null, null, null, null));
            }
        }

        return new EnvironmentComparison(
            Guid.NewGuid(),
            baseline.SessionId,
            baseline.Id,
            comparison.Id,
            comparedAt,
            changes.OrderBy(change => change.TargetDisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(change => change.Kind).ToArray(),
            baseline.Warnings.Concat(comparison.Warnings).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void AddVariableChanges(
        EnvironmentVariableSnapshot before,
        EnvironmentVariableSnapshot after,
        List<EnvironmentVariableChange> changes)
    {
        if (!before.IsPath)
        {
            if (!string.Equals(before.RawValue, after.RawValue, StringComparison.Ordinal) ||
                before.ValueType != after.ValueType)
            {
                changes.Add(CreateChange(EnvironmentVariableChangeKind.ValueChanged, after.Scope, after.Name, before, after, null, null, null));
            }

            return;
        }

        AddPathEntryChanges(before, after, changes);

        if (!changes.Any(change =>
                change.Scope == after.Scope &&
                string.Equals(change.Name, after.Name, StringComparison.OrdinalIgnoreCase)) &&
            !string.Equals(before.RawValue, after.RawValue, StringComparison.Ordinal))
        {
            changes.Add(CreateChange(EnvironmentVariableChangeKind.ValueChanged, after.Scope, after.Name, before, after, null, null, null));
        }
    }

    private static void AddPathEntryChanges(
        EnvironmentVariableSnapshot before,
        EnvironmentVariableSnapshot after,
        List<EnvironmentVariableChange> changes)
    {
        var beforeEntries = IndexedPathEntries(before.PathEntries);
        var afterEntries = IndexedPathEntries(after.PathEntries);

        foreach (var (key, afterEntry) in afterEntries)
        {
            if (!beforeEntries.ContainsKey(key))
            {
                changes.Add(CreateChange(
                    EnvironmentVariableChangeKind.PathEntryAdded,
                    after.Scope,
                    after.Name,
                    before,
                    after,
                    afterEntry.Value,
                    null,
                    afterEntry.Index));
            }
        }

        foreach (var (key, beforeEntry) in beforeEntries)
        {
            if (!afterEntries.ContainsKey(key))
            {
                changes.Add(CreateChange(
                    EnvironmentVariableChangeKind.PathEntryRemoved,
                    before.Scope,
                    before.Name,
                    before,
                    after,
                    beforeEntry.Value,
                    beforeEntry.Index,
                    null));
            }
        }

        AddPathReorderChanges(before, after, beforeEntries, afterEntries, changes);
    }

    private static void AddPathReorderChanges(
        EnvironmentVariableSnapshot before,
        EnvironmentVariableSnapshot after,
        IReadOnlyDictionary<string, IndexedPathEntry> beforeEntries,
        IReadOnlyDictionary<string, IndexedPathEntry> afterEntries,
        List<EnvironmentVariableChange> changes)
    {
        var commonBefore = before.PathEntries
            .Select(IndexedPathEntryKeyFactory())
            .Where(key => beforeEntries.ContainsKey(key) && afterEntries.ContainsKey(key))
            .ToArray();
        var commonAfter = after.PathEntries
            .Select(IndexedPathEntryKeyFactory())
            .Where(key => beforeEntries.ContainsKey(key) && afterEntries.ContainsKey(key))
            .ToArray();

        for (var index = 0; index < commonAfter.Length; index++)
        {
            var key = commonAfter[index];
            var beforeOrder = Array.FindIndex(commonBefore, value => string.Equals(value, key, StringComparison.OrdinalIgnoreCase));
            if (beforeOrder < 0 || beforeOrder == index)
            {
                continue;
            }

            var beforeEntry = beforeEntries[key];
            var afterEntry = afterEntries[key];
            changes.Add(CreateChange(
                EnvironmentVariableChangeKind.PathEntryReordered,
                before.Scope,
                before.Name,
                before,
                after,
                afterEntry.Value,
                beforeEntry.Index,
                afterEntry.Index));
        }
    }

    private static Dictionary<string, IndexedPathEntry> IndexedPathEntries(IReadOnlyList<string> entries)
    {
        var result = new Dictionary<string, IndexedPathEntry>(StringComparer.OrdinalIgnoreCase);
        var keyFactory = IndexedPathEntryKeyFactory();

        for (var index = 0; index < entries.Count; index++)
        {
            var value = entries[index].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result.Add(keyFactory(value), new IndexedPathEntry(value, index));
        }

        return result;
    }

    private static Func<string, string> IndexedPathEntryKeyFactory()
    {
        var occurrenceByIdentity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        return value =>
        {
            var identity = PathEntryIdentity(value);
            occurrenceByIdentity.TryGetValue(identity, out var occurrence);
            occurrenceByIdentity[identity] = occurrence + 1;
            return $"{identity}\u001F{occurrence}";
        };
    }

    private static EnvironmentVariableChange CreateChange(
        EnvironmentVariableChangeKind kind,
        EnvironmentVariableScopeKind scope,
        string name,
        EnvironmentVariableSnapshot? before,
        EnvironmentVariableSnapshot? after,
        string? pathEntry,
        int? beforeIndex,
        int? afterIndex)
    {
        var availability = EnvironmentChangeExplainer.GetRollbackAvailability(kind, before, after);

        return new EnvironmentVariableChange(
            Guid.NewGuid(),
            kind,
            scope,
            name,
            before,
            after,
            pathEntry,
            beforeIndex,
            afterIndex,
            EnvironmentChangeExplainer.Summarize(kind, scope, name, before, after, pathEntry, beforeIndex, afterIndex),
            EnvironmentChangeExplainer.Classify(kind, scope, name, availability),
            availability);
    }

    private static string VariableIdentity(EnvironmentVariableSnapshot variable)
    {
        return $"{variable.Scope}|{variable.Name}";
    }

    private static string PathEntryIdentity(string value)
    {
        return value.Trim().TrimEnd('\\', '/');
    }

    private sealed record IndexedPathEntry(string Value, int Index);
}
