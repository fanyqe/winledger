using Microsoft.Win32;
using WinLedger.Core.Abstractions;
using WinLedger.Core.Registry;
using WinLedger.Domain.Registry;

namespace WinLedger.Windows.Registry;

public sealed class WindowsRegistrySnapshotCollector(IClock clock) : IRegistrySnapshotCollector
{
    public Task<RegistrySnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        IReadOnlyList<RegistrySnapshotTarget> targets,
        CancellationToken cancellationToken)
    {
        var keys = new List<RegistryKeySnapshot>();
        var warnings = new List<string>();

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var root = OpenBaseKey(target.Path);
                using var key = string.IsNullOrEmpty(target.Path.KeyPath)
                    ? root
                    : root.OpenSubKey(target.Path.KeyPath, writable: false);

                if (key is null)
                {
                    warnings.Add($"Registry key was not found: {target.Path}");
                    continue;
                }

                CaptureKey(target.Path, key, target.IncludeSubKeys, keys, warnings, cancellationToken);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                warnings.Add($"Registry collection failed for {target.Path}: {ex.Message}");
            }
        }

        var snapshot = new RegistrySnapshot(
            Guid.NewGuid(),
            sessionId,
            snapshotName,
            clock.UtcNow,
            targets,
            keys.OrderBy(key => key.Path.FullPath, StringComparer.OrdinalIgnoreCase).ToArray(),
            warnings);

        return Task.FromResult(snapshot);
    }

    private static void CaptureKey(
        RegistryPath path,
        RegistryKey key,
        bool includeSubKeys,
        List<RegistryKeySnapshot> keys,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var values = new List<RegistryValueSnapshot>();
        foreach (var valueName in key.GetValueNames().Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var kind = key.GetValueKind(valueName);
                var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                values.Add(RegistryValueCodec.FromWindowsValue(valueName, value, kind));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                warnings.Add($"Registry value could not be read: {path}\\{valueName}: {ex.Message}");
            }
        }

        keys.Add(new RegistryKeySnapshot(path, values));

        if (!includeSubKeys)
        {
            return;
        }

        foreach (var subKeyName in key.GetSubKeyNames().Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var subKey = key.OpenSubKey(subKeyName, writable: false);
                if (subKey is null)
                {
                    warnings.Add($"Registry subkey disappeared during collection: {path}\\{subKeyName}");
                    continue;
                }

                CaptureKey(
                    new RegistryPath(path.Hive, string.IsNullOrEmpty(path.KeyPath) ? subKeyName : $"{path.KeyPath}\\{subKeyName}", path.View),
                    subKey,
                    true,
                    keys,
                    warnings,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                warnings.Add($"Registry subkey could not be read: {path}\\{subKeyName}: {ex.Message}");
            }
        }
    }

    private static RegistryKey OpenBaseKey(RegistryPath path)
    {
        return RegistryKey.OpenBaseKey(ToWindowsHive(path.Hive), ToWindowsView(path.View));
    }

    private static RegistryHive ToWindowsHive(RegistryHiveKind hive)
    {
        return hive switch
        {
            RegistryHiveKind.CurrentUser => RegistryHive.CurrentUser,
            RegistryHiveKind.LocalMachine => RegistryHive.LocalMachine,
            RegistryHiveKind.ClassesRoot => RegistryHive.ClassesRoot,
            RegistryHiveKind.Users => RegistryHive.Users,
            RegistryHiveKind.CurrentConfig => RegistryHive.CurrentConfig,
            _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, "Unsupported registry hive.")
        };
    }

    private static RegistryView ToWindowsView(RegistryViewKind view)
    {
        return view switch
        {
            RegistryViewKind.Default => RegistryView.Default,
            RegistryViewKind.Registry32 => RegistryView.Registry32,
            RegistryViewKind.Registry64 => RegistryView.Registry64,
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unsupported registry view.")
        };
    }
}
