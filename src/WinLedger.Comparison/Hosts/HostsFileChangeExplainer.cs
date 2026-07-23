using WinLedger.Domain.Hosts;
using WinLedger.Domain.Rollback;

namespace WinLedger.Comparison.Hosts;

public static class HostsFileChangeExplainer
{
    public static string Summarize(
        HostsFileChangeKind kind,
        string filePath,
        HostsFileLineSnapshot? beforeLine,
        HostsFileLineSnapshot? afterLine)
    {
        return kind switch
        {
            HostsFileChangeKind.FileCreated => $"The hosts file was created at {Display(filePath)}.",
            HostsFileChangeKind.FileRemoved => $"The hosts file was removed from {Display(filePath)}.",
            HostsFileChangeKind.ContentChanged => "The hosts file content changed without line-level additions or removals.",
            HostsFileChangeKind.LineAdded => $"The hosts file line {DisplayLine(afterLine)} was added.",
            HostsFileChangeKind.LineRemoved => $"The hosts file line {DisplayLine(beforeLine)} was removed.",
            _ => "The hosts file changed."
        };
    }

    public static IReadOnlySet<ChangeAttentionLabel> Classify(
        HostsFileChangeKind kind,
        RollbackAvailability rollbackAvailability)
    {
        var labels = new HashSet<ChangeAttentionLabel>
        {
            ChangeAttentionLabel.Persistent,
            ChangeAttentionLabel.NetworkRelated,
            ChangeAttentionLabel.SecuritySensitive
        };

        if (kind is HostsFileChangeKind.FileRemoved or HostsFileChangeKind.ContentChanged or HostsFileChangeKind.LineRemoved)
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
        HostsFileChangeKind kind,
        HostsFileSnapshot? before,
        HostsFileSnapshot? after)
    {
        return kind switch
        {
            HostsFileChangeKind.FileCreated when after is { Exists: true } => RollbackAvailability.RequiresConfirmation,
            HostsFileChangeKind.FileRemoved when before is { Exists: true } => RollbackAvailability.RequiresConfirmation,
            HostsFileChangeKind.ContentChanged or HostsFileChangeKind.LineAdded or HostsFileChangeKind.LineRemoved
                when before is { Exists: true, ContentBase64: not null } && after is { Exists: true, ContentBase64: not null } => RollbackAvailability.RequiresConfirmation,
            _ => RollbackAvailability.ManualReview
        };
    }

    private static string Display(string value)
    {
        return $"\"{value}\"";
    }

    private static string DisplayLine(HostsFileLineSnapshot? line)
    {
        return line is null ? "(unknown)" : $"{line.LineNumber}: {Display(line.Text)}";
    }
}
