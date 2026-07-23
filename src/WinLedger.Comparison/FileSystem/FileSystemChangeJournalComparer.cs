using WinLedger.Domain.FileSystem;

namespace WinLedger.Comparison.FileSystem;

internal static class FileSystemChangeJournalComparer
{
    public static IReadOnlyList<string> Compare(FileSystemSnapshot baseline, FileSystemSnapshot comparison)
    {
        if (baseline.ChangeJournalStates.Count == 0 && comparison.ChangeJournalStates.Count == 0)
        {
            return Array.Empty<string>();
        }

        var warnings = new List<string>();
        var comparisonStates = comparison.ChangeJournalStates
            .Where(state => state.IsAvailable)
            .ToDictionary(state => state.VolumeRootPath, StringComparer.OrdinalIgnoreCase);

        foreach (var baselineState in baseline.ChangeJournalStates.Where(state => state.IsAvailable))
        {
            if (!comparisonStates.TryGetValue(baselineState.VolumeRootPath, out var comparisonState))
            {
                warnings.Add($"Change journal state is missing for volume {baselineState.VolumeRootPath} in the comparison snapshot.");
                continue;
            }

            if (baselineState.JournalId != comparisonState.JournalId)
            {
                warnings.Add($"Change journal was recreated for volume {baselineState.VolumeRootPath}; journal continuity could not be verified.");
                continue;
            }

            if (baselineState.NextUsn.HasValue &&
                comparisonState.LowestValidUsn.HasValue &&
                baselineState.NextUsn.Value < comparisonState.LowestValidUsn.Value)
            {
                warnings.Add($"Change journal records needed for volume {baselineState.VolumeRootPath} were trimmed before comparison.");
            }

            if (baselineState.NextUsn.HasValue &&
                comparisonState.NextUsn.HasValue &&
                comparisonState.NextUsn.Value < baselineState.NextUsn.Value)
            {
                warnings.Add($"Change journal moved backwards for volume {baselineState.VolumeRootPath}; journal continuity could not be verified.");
            }
        }

        return warnings.Distinct(StringComparer.Ordinal).ToArray();
    }
}
