namespace WinLedger.Domain.FileSystem;

public sealed record FileSystemSnapshotOptions(
    IReadOnlyList<string> MonitoredRoots,
    IReadOnlyList<string> ExclusionPatterns,
    bool IncludeHighNoise,
    bool CalculateHashes,
    bool BackupSmallFiles,
    long BackupSizeLimitBytes)
{
    public static readonly IReadOnlyList<string> DefaultExclusionPatterns =
    [
        @"\$Recycle.Bin\",
        @"\AppData\Local\Temp\",
        @"\AppData\Local\Microsoft\Windows\INetCache\",
        @"\AppData\Local\Microsoft\Windows\Explorer\",
        @"\Temporary Internet Files\",
        @"\Cache\",
        @"\Caches\",
        @"\Logs\",
        @"\Windows\SoftwareDistribution\Download\",
        @"\pagefile.sys",
        @"\hiberfil.sys",
        @"\swapfile.sys"
    ];

    public static FileSystemSnapshotOptions ForRoots(params string[] roots)
    {
        return new FileSystemSnapshotOptions(
            roots,
            DefaultExclusionPatterns,
            false,
            false,
            false,
            0);
    }
}
