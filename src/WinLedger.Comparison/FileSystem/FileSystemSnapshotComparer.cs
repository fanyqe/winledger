using WinLedger.Domain.FileSystem;

namespace WinLedger.Comparison.FileSystem;

public sealed class FileSystemSnapshotComparer
{
    public FileSystemComparison Compare(
        FileSystemSnapshot baseline,
        FileSystemSnapshot comparison,
        DateTimeOffset comparedAt)
    {
        if (baseline.SessionId != comparison.SessionId)
        {
            throw new ArgumentException("File-system snapshots belong to different sessions.", nameof(comparison));
        }

        var baselineEntries = baseline.Entries.ToDictionary(entry => entry.Path, StringComparer.OrdinalIgnoreCase);
        var comparisonEntries = comparison.Entries.ToDictionary(entry => entry.Path, StringComparer.OrdinalIgnoreCase);
        var removed = baselineEntries.Values
            .Where(entry => !comparisonEntries.ContainsKey(entry.Path))
            .ToList();
        var created = comparisonEntries.Values
            .Where(entry => !baselineEntries.ContainsKey(entry.Path))
            .ToList();
        var renamedPairs = PairRenames(removed, created);
        var pairedRemoved = renamedPairs.Select(pair => pair.Before.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pairedCreated = renamedPairs.Select(pair => pair.After.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changes = new List<FileSystemChange>();

        foreach (var pair in renamedPairs)
        {
            changes.Add(CreateChange(
                FileSystemChangeKind.Renamed,
                pair.Before,
                pair.After,
                pair.After.Path,
                pair.Before.Path));
        }

        foreach (var after in created.Where(entry => !pairedCreated.Contains(entry.Path)))
        {
            changes.Add(CreateChange(FileSystemChangeKind.Created, null, after, after.Path, null));
        }

        foreach (var before in removed.Where(entry => !pairedRemoved.Contains(entry.Path)))
        {
            changes.Add(CreateChange(FileSystemChangeKind.Deleted, before, null, before.Path, null));
        }

        foreach (var (path, after) in comparisonEntries)
        {
            if (baselineEntries.TryGetValue(path, out var before) && EntryChanged(before, after))
            {
                changes.Add(CreateChange(FileSystemChangeKind.Modified, before, after, path, null));
            }
        }

        var warnings = baseline.Warnings
            .Concat(comparison.Warnings)
            .Concat(FileSystemChangeJournalComparer.Compare(baseline, comparison))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new FileSystemComparison(
            Guid.NewGuid(),
            baseline.SessionId,
            baseline.Id,
            comparison.Id,
            comparedAt,
            changes.OrderBy(change => change.TargetDisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(change => change.Kind).ToArray(),
            warnings);
    }

    private static IReadOnlyList<(FileSystemEntrySnapshot Before, FileSystemEntrySnapshot After)> PairRenames(
        IReadOnlyList<FileSystemEntrySnapshot> removed,
        IReadOnlyList<FileSystemEntrySnapshot> created)
    {
        var pairs = new List<(FileSystemEntrySnapshot Before, FileSystemEntrySnapshot After)>();
        var unmatchedCreated = created.ToList();

        foreach (var before in removed.Where(entry => entry.Kind == FileSystemEntryKind.File && !string.IsNullOrWhiteSpace(entry.Sha256)))
        {
            var match = unmatchedCreated.FirstOrDefault(after =>
                after.Kind == FileSystemEntryKind.File &&
                string.Equals(before.Sha256, after.Sha256, StringComparison.OrdinalIgnoreCase) &&
                before.SizeBytes == after.SizeBytes);

            if (match is null)
            {
                continue;
            }

            pairs.Add((before, match));
            unmatchedCreated.Remove(match);
        }

        return pairs;
    }

    private static bool EntryChanged(FileSystemEntrySnapshot before, FileSystemEntrySnapshot after)
    {
        if (before.Kind != after.Kind)
        {
            return true;
        }

        if (before.SizeBytes != after.SizeBytes ||
            before.LastWriteTimeUtc != after.LastWriteTimeUtc ||
            !string.Equals(before.Attributes, after.Attributes, StringComparison.Ordinal) ||
            !string.Equals(before.Sha256, after.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return before.IsTemporaryOrHighNoise != after.IsTemporaryOrHighNoise;
    }

    private static FileSystemChange CreateChange(
        FileSystemChangeKind kind,
        FileSystemEntrySnapshot? before,
        FileSystemEntrySnapshot? after,
        string path,
        string? previousPath)
    {
        var availability = FileSystemChangeExplainer.GetRollbackAvailability(kind, before, after);
        var entryKind = after?.Kind ?? before?.Kind ?? FileSystemEntryKind.File;

        return new FileSystemChange(
            Guid.NewGuid(),
            kind,
            entryKind,
            path,
            previousPath,
            before,
            after,
            FileSystemChangeExplainer.Summarize(kind, before, after, path, previousPath),
            FileSystemChangeExplainer.Classify(kind, before, after, availability),
            availability);
    }
}
