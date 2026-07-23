using WinLedger.Domain.FileSystem;
using WinLedger.Domain.Rollback;

namespace WinLedger.Comparison.FileSystem;

public static class FileSystemChangeExplainer
{
    public static string Summarize(
        FileSystemChangeKind kind,
        FileSystemEntrySnapshot? before,
        FileSystemEntrySnapshot? after,
        string path,
        string? previousPath)
    {
        return kind switch
        {
            FileSystemChangeKind.Created => $"{DisplayKind(after?.Kind)} was created at \"{path}\".",
            FileSystemChangeKind.Deleted => $"{DisplayKind(before?.Kind)} was deleted from \"{path}\".",
            FileSystemChangeKind.Modified => $"The file \"{path}\" changed.",
            FileSystemChangeKind.Renamed => $"The file \"{previousPath}\" was renamed to \"{path}\".",
            _ => $"The file-system entry \"{path}\" changed."
        };
    }

    public static IReadOnlySet<ChangeAttentionLabel> Classify(
        FileSystemChangeKind kind,
        FileSystemEntrySnapshot? before,
        FileSystemEntrySnapshot? after,
        RollbackAvailability rollbackAvailability)
    {
        var labels = new HashSet<ChangeAttentionLabel>
        {
            ChangeAttentionLabel.Persistent
        };

        if (kind is FileSystemChangeKind.Deleted or FileSystemChangeKind.Modified or FileSystemChangeKind.Renamed)
        {
            labels.Add(ChangeAttentionLabel.PotentiallyDestructive);
        }

        if ((after ?? before)?.IsTemporaryOrHighNoise == true)
        {
            labels.Add(ChangeAttentionLabel.Informational);
        }

        if (rollbackAvailability is RollbackAvailability.Unavailable or RollbackAvailability.ManualReview)
        {
            labels.Add(ChangeAttentionLabel.RollbackUnavailable);
        }

        return labels;
    }

    public static RollbackAvailability GetRollbackAvailability(
        FileSystemChangeKind kind,
        FileSystemEntrySnapshot? before,
        FileSystemEntrySnapshot? after)
    {
        return kind switch
        {
            FileSystemChangeKind.Created => RollbackAvailability.RequiresConfirmation,
            FileSystemChangeKind.Deleted when before?.Kind == FileSystemEntryKind.File && before.HasRollbackData => RollbackAvailability.RequiresConfirmation,
            FileSystemChangeKind.Modified when before?.Kind == FileSystemEntryKind.File && before.HasRollbackData && after is not null => RollbackAvailability.RequiresConfirmation,
            _ => RollbackAvailability.ManualReview
        };
    }

    private static string DisplayKind(FileSystemEntryKind? kind)
    {
        return kind == FileSystemEntryKind.Directory ? "A directory" : "A file";
    }
}
