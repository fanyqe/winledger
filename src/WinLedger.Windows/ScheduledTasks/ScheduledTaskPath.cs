namespace WinLedger.Windows.ScheduledTasks;

internal static class ScheduledTaskPath
{
    public static (string FolderPath, string TaskName) Split(string taskPath)
    {
        if (string.IsNullOrWhiteSpace(taskPath))
        {
            throw new ArgumentException("Scheduled task path cannot be empty.", nameof(taskPath));
        }

        var normalized = taskPath.Replace('/', '\\').Trim();
        if (!normalized.StartsWith('\\'))
        {
            normalized = $"\\{normalized}";
        }

        var lastSlash = normalized.LastIndexOf('\\');
        if (lastSlash <= 0 || lastSlash == normalized.Length - 1)
        {
            return ("\\", normalized.Trim('\\'));
        }

        return (normalized[..lastSlash], normalized[(lastSlash + 1)..]);
    }

    public static string Combine(string folderPath, string taskName)
    {
        var normalizedFolder = string.IsNullOrWhiteSpace(folderPath)
            ? "\\"
            : folderPath.Replace('/', '\\').TrimEnd('\\');

        if (!normalizedFolder.StartsWith('\\'))
        {
            normalizedFolder = $"\\{normalizedFolder}";
        }

        return normalizedFolder == "\\"
            ? $"\\{taskName}"
            : $"{normalizedFolder}\\{taskName}";
    }
}
