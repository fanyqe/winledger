using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using WinLedger.Domain.FileSystem;

namespace WinLedger.Windows.FileSystem;

internal static class WindowsChangeJournalReader
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FsctlQueryUsnJournal = 0x000900f4;

    public static FileSystemChangeJournalState CaptureState(string rootPath)
    {
        var volumeRoot = ResolveVolumeRootPath(rootPath);
        if (string.IsNullOrWhiteSpace(volumeRoot))
        {
            return Unavailable(rootPath, string.Empty, "A local volume root could not be resolved.");
        }

        var fileSystemName = GetFileSystemName(volumeRoot);
        var devicePath = TryCreateVolumeDevicePath(volumeRoot);
        if (devicePath is null)
        {
            return Unavailable(volumeRoot, fileSystemName, "The root is not a local Windows volume.");
        }

        using var volume = CreateFileW(
            devicePath,
            GenericRead,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);

        if (volume.IsInvalid)
        {
            return Unavailable(volumeRoot, fileSystemName, LastErrorMessage("The change journal could not be opened"));
        }

        var outputSize = (uint)Marshal.SizeOf<UsnJournalDataV0>();
        var success = DeviceIoControl(
            volume,
            FsctlQueryUsnJournal,
            IntPtr.Zero,
            0,
            out var data,
            outputSize,
            out _,
            IntPtr.Zero);

        if (!success)
        {
            return Unavailable(volumeRoot, fileSystemName, LastErrorMessage("The change journal could not be queried"));
        }

        return new FileSystemChangeJournalState(
            volumeRoot,
            fileSystemName,
            true,
            data.UsnJournalId,
            data.FirstUsn,
            data.NextUsn,
            data.LowestValidUsn,
            data.MaxUsn,
            null);
    }

    private static string ResolveVolumeRootPath(string rootPath)
    {
        var fullPath = Path.GetFullPath(rootPath);
        var builder = new StringBuilder(512);
        return GetVolumePathNameW(fullPath, builder, builder.Capacity)
            ? builder.ToString()
            : Path.GetPathRoot(fullPath) ?? string.Empty;
    }

    private static string GetFileSystemName(string volumeRoot)
    {
        var builder = new StringBuilder(256);
        return GetVolumeInformationW(
            volumeRoot,
            null,
            0,
            out _,
            out _,
            out _,
            builder,
            builder.Capacity)
            ? builder.ToString()
            : string.Empty;
    }

    private static string? TryCreateVolumeDevicePath(string volumeRoot)
    {
        if (volumeRoot.StartsWith(@"\\", StringComparison.Ordinal) &&
            !volumeRoot.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            return null;
        }

        var root = volumeRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (root.Length == 2 && root[1] == ':')
        {
            return $@"\\.\{root}";
        }

        if (volumeRoot.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        var volumeName = new StringBuilder(512);
        if (!GetVolumeNameForVolumeMountPointW(volumeRoot, volumeName, volumeName.Capacity))
        {
            return null;
        }

        return volumeName.ToString().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static FileSystemChangeJournalState Unavailable(
        string volumeRoot,
        string fileSystemName,
        string reason)
    {
        return new FileSystemChangeJournalState(
            volumeRoot,
            fileSystemName,
            false,
            null,
            null,
            null,
            null,
            null,
            reason);
    }

    private static string LastErrorMessage(string prefix)
    {
        var error = Marshal.GetLastPInvokeError();
        return error == 0
            ? prefix
            : $"{prefix}: {new Win32Exception(error).Message}";
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        IntPtr inBuffer,
        uint inBufferSize,
        out UsnJournalDataV0 outBuffer,
        uint outBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "GetVolumePathNameW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetVolumePathNameW(
        string fileName,
        StringBuilder volumePathName,
        int bufferLength);

    [DllImport("kernel32.dll", EntryPoint = "GetVolumeInformationW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetVolumeInformationW(
        string rootPathName,
        StringBuilder? volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int fileSystemNameSize);

    [DllImport("kernel32.dll", EntryPoint = "GetVolumeNameForVolumeMountPointW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetVolumeNameForVolumeMountPointW(
        string volumeMountPoint,
        StringBuilder volumeName,
        int bufferLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct UsnJournalDataV0
    {
        public ulong UsnJournalId;
        public long FirstUsn;
        public long NextUsn;
        public long LowestValidUsn;
        public long MaxUsn;
        public ulong MaximumSize;
        public ulong AllocationDelta;
    }
}
