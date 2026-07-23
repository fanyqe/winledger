using WinLedger.Domain.Hosts;

namespace WinLedger.Comparison.Hosts;

public sealed class HostsFileSnapshotComparer
{
    public HostsFileComparison Compare(HostsFileSnapshot baseline, HostsFileSnapshot comparison, DateTimeOffset comparedAt)
    {
        if (baseline.SessionId != comparison.SessionId)
        {
            throw new ArgumentException("Hosts file snapshots belong to different sessions.", nameof(comparison));
        }

        var changes = new List<HostsFileChange>();
        if (!baseline.Exists && comparison.Exists)
        {
            changes.Add(CreateChange(HostsFileChangeKind.FileCreated, comparison.FilePath, null, null, baseline, comparison));
        }
        else if (baseline.Exists && !comparison.Exists)
        {
            changes.Add(CreateChange(HostsFileChangeKind.FileRemoved, baseline.FilePath, null, null, baseline, comparison));
        }
        else if (baseline.Exists && comparison.Exists && ContentChanged(baseline, comparison))
        {
            AddLineChanges(baseline, comparison, changes);
            if (changes.Count == 0)
            {
                changes.Add(CreateChange(HostsFileChangeKind.ContentChanged, baseline.FilePath, null, null, baseline, comparison));
            }
        }

        return new HostsFileComparison(
            Guid.NewGuid(),
            baseline.SessionId,
            baseline.Id,
            comparison.Id,
            comparedAt,
            changes.OrderBy(change => change.TargetDisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(change => change.Kind).ToArray(),
            baseline.Warnings.Concat(comparison.Warnings).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static bool ContentChanged(HostsFileSnapshot baseline, HostsFileSnapshot comparison)
    {
        return !string.Equals(baseline.Content, comparison.Content, StringComparison.Ordinal) ||
               !string.Equals(baseline.ContentBase64, comparison.ContentBase64, StringComparison.Ordinal) ||
               !string.Equals(baseline.ContentSha256, comparison.ContentSha256, StringComparison.OrdinalIgnoreCase) ||
               baseline.Length != comparison.Length;
    }

    private static void AddLineChanges(
        HostsFileSnapshot baseline,
        HostsFileSnapshot comparison,
        List<HostsFileChange> changes)
    {
        var beforeLines = IndexedLines(baseline.Lines);
        var afterLines = IndexedLines(comparison.Lines);

        foreach (var (key, afterLine) in afterLines)
        {
            if (!beforeLines.ContainsKey(key))
            {
                changes.Add(CreateChange(HostsFileChangeKind.LineAdded, comparison.FilePath, null, afterLine, baseline, comparison));
            }
        }

        foreach (var (key, beforeLine) in beforeLines)
        {
            if (!afterLines.ContainsKey(key))
            {
                changes.Add(CreateChange(HostsFileChangeKind.LineRemoved, baseline.FilePath, beforeLine, null, baseline, comparison));
            }
        }
    }

    private static Dictionary<string, HostsFileLineSnapshot> IndexedLines(IReadOnlyList<HostsFileLineSnapshot> lines)
    {
        var result = new Dictionary<string, HostsFileLineSnapshot>(StringComparer.Ordinal);
        var occurrenceByText = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            occurrenceByText.TryGetValue(line.Text, out var occurrence);
            occurrenceByText[line.Text] = occurrence + 1;
            result.Add($"{line.Text}\u001F{occurrence}", line);
        }

        return result;
    }

    private static HostsFileChange CreateChange(
        HostsFileChangeKind kind,
        string filePath,
        HostsFileLineSnapshot? beforeLine,
        HostsFileLineSnapshot? afterLine,
        HostsFileSnapshot? before,
        HostsFileSnapshot? after)
    {
        var availability = HostsFileChangeExplainer.GetRollbackAvailability(kind, before, after);

        return new HostsFileChange(
            Guid.NewGuid(),
            kind,
            filePath,
            beforeLine,
            afterLine,
            before,
            after,
            HostsFileChangeExplainer.Summarize(kind, filePath, beforeLine, afterLine),
            HostsFileChangeExplainer.Classify(kind, availability),
            availability);
    }
}
