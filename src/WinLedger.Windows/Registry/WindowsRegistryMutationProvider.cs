using Microsoft.Win32;
using WinLedger.Core.Registry;
using WinLedger.Domain.Registry;

namespace WinLedger.Windows.Registry;

public sealed class WindowsRegistryMutationProvider : IRegistryMutationProvider
{
    public Task<RegistryValueSnapshot?> ReadValueAsync(
        RegistryPath keyPath,
        string valueName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var root = RegistryKey.OpenBaseKey(ToWindowsHive(keyPath.Hive), ToWindowsView(keyPath.View));
        using var key = root.OpenSubKey(keyPath.KeyPath, writable: false);
        if (key is null)
        {
            return Task.FromResult<RegistryValueSnapshot?>(null);
        }

        var names = key.GetValueNames();
        if (!names.Any(name => string.Equals(name, valueName, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult<RegistryValueSnapshot?>(null);
        }

        var kind = key.GetValueKind(valueName);
        var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return Task.FromResult<RegistryValueSnapshot?>(RegistryValueCodec.FromWindowsValue(valueName, value, kind));
    }

    public Task SetValueAsync(
        RegistryPath keyPath,
        RegistryValueSnapshot value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var root = RegistryKey.OpenBaseKey(ToWindowsHive(keyPath.Hive), ToWindowsView(keyPath.View));
        using var key = root.CreateSubKey(keyPath.KeyPath, writable: true)
            ?? throw new InvalidOperationException($"Registry key could not be opened for writing: {keyPath}");

        var windowsValue = RegistryValueCodec.ToWindowsValue(value);
        key.SetValue(value.Name, windowsValue.Value, windowsValue.Kind);
        return Task.CompletedTask;
    }

    public Task DeleteValueAsync(
        RegistryPath keyPath,
        string valueName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var root = RegistryKey.OpenBaseKey(ToWindowsHive(keyPath.Hive), ToWindowsView(keyPath.View));
        using var key = root.OpenSubKey(keyPath.KeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
        return Task.CompletedTask;
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
