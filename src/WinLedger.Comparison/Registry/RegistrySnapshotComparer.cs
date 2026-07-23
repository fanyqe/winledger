using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;

namespace WinLedger.Comparison.Registry;

public sealed class RegistrySnapshotComparer
{
    public RegistryComparison Compare(RegistrySnapshot baseline, RegistrySnapshot comparison, DateTimeOffset comparedAt)
    {
        if (baseline.SessionId != comparison.SessionId)
        {
            throw new ArgumentException("Registry snapshots belong to different sessions.", nameof(comparison));
        }

        var changes = new List<RegistryChange>();
        var baselineKeys = baseline.Keys.ToDictionary(key => KeyIdentity(key.Path), StringComparer.OrdinalIgnoreCase);
        var comparisonKeys = comparison.Keys.ToDictionary(key => KeyIdentity(key.Path), StringComparer.OrdinalIgnoreCase);

        foreach (var (key, afterKey) in comparisonKeys)
        {
            if (!baselineKeys.TryGetValue(key, out var beforeKey))
            {
                changes.Add(CreateKeyChange(RegistryChangeKind.KeyCreated, afterKey.Path));
                foreach (var value in afterKey.Values)
                {
                    changes.Add(CreateValueChange(RegistryChangeKind.ValueCreated, afterKey.Path, value.Name, null, value));
                }
            }
            else
            {
                AddValueChanges(beforeKey, afterKey, changes);
            }
        }

        foreach (var (key, beforeKey) in baselineKeys)
        {
            if (!comparisonKeys.ContainsKey(key))
            {
                changes.Add(CreateKeyChange(RegistryChangeKind.KeyRemoved, beforeKey.Path));
                foreach (var value in beforeKey.Values)
                {
                    changes.Add(CreateValueChange(RegistryChangeKind.ValueRemoved, beforeKey.Path, value.Name, value, null));
                }
            }
        }

        return new RegistryComparison(
            Guid.NewGuid(),
            baseline.SessionId,
            baseline.Id,
            comparison.Id,
            comparedAt,
            changes.OrderBy(change => change.TargetDisplayName, StringComparer.OrdinalIgnoreCase).ToArray(),
            baseline.Warnings.Concat(comparison.Warnings).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void AddValueChanges(
        RegistryKeySnapshot baselineKey,
        RegistryKeySnapshot comparisonKey,
        List<RegistryChange> changes)
    {
        var beforeValues = baselineKey.Values.ToDictionary(value => value.Name, StringComparer.OrdinalIgnoreCase);
        var afterValues = comparisonKey.Values.ToDictionary(value => value.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, after) in afterValues)
        {
            if (!beforeValues.TryGetValue(name, out var before))
            {
                changes.Add(CreateValueChange(RegistryChangeKind.ValueCreated, comparisonKey.Path, name, null, after));
                continue;
            }

            if (before.ValueType != after.ValueType)
            {
                changes.Add(CreateValueChange(RegistryChangeKind.ValueTypeChanged, comparisonKey.Path, name, before, after));
                continue;
            }

            if (!string.Equals(before.SerializedValue, after.SerializedValue, StringComparison.Ordinal))
            {
                changes.Add(CreateValueChange(RegistryChangeKind.ValueModified, comparisonKey.Path, name, before, after));
            }
        }

        foreach (var (name, before) in beforeValues)
        {
            if (!afterValues.ContainsKey(name))
            {
                changes.Add(CreateValueChange(RegistryChangeKind.ValueRemoved, baselineKey.Path, name, before, null));
            }
        }
    }

    private static RegistryChange CreateKeyChange(RegistryChangeKind kind, RegistryPath path)
    {
        var availability = kind == RegistryChangeKind.KeyCreated
            ? RollbackAvailability.ManualReview
            : RollbackAvailability.Unavailable;

        return new RegistryChange(
            Guid.NewGuid(),
            kind,
            path,
            null,
            null,
            null,
            RegistryChangeExplainer.SummarizeKeyChange(kind, path),
            RegistryChangeExplainer.Classify(path, null, availability),
            availability);
    }

    private static RegistryChange CreateValueChange(
        RegistryChangeKind kind,
        RegistryPath path,
        string name,
        RegistryValueSnapshot? before,
        RegistryValueSnapshot? after)
    {
        const RollbackAvailability availability = RollbackAvailability.Automatic;

        return new RegistryChange(
            Guid.NewGuid(),
            kind,
            path,
            name,
            before,
            after,
            RegistryChangeExplainer.SummarizeValueChange(kind, path, name, before, after),
            RegistryChangeExplainer.Classify(path, name, availability),
            availability);
    }

    private static string KeyIdentity(RegistryPath path)
    {
        return $"{path.View}|{path.FullPath}";
    }
}
