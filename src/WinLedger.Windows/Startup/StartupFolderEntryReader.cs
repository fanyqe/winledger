using WinLedger.Domain.Startup;

namespace WinLedger.Windows.Startup;

internal static class StartupFolderEntryReader
{
    public static StartupEntrySnapshot? ReadFile(string filePath)
    {
        var file = new FileInfo(filePath);
        if (!file.Exists)
        {
            return null;
        }

        return new StartupEntrySnapshot(
            $"StartupFolder|{file.FullName}",
            StartupEntrySourceKind.StartupFolder,
            file.Name,
            file.FullName,
            file.FullName,
            true,
            null,
            "Startup folder entry",
            "StartupFolder",
            file.Length,
            file.LastWriteTimeUtc);
    }
}
