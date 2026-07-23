using Microsoft.Win32;
using WinLedger.Domain.Services;

namespace WinLedger.Windows.Services;

internal static class ServiceRegistryReader
{
    private const string ServicesRootPath = @"SYSTEM\CurrentControlSet\Services";

    public static WindowsServiceSnapshot ReadServiceSnapshot(
        string serviceName,
        string? fallbackDisplayName,
        ServiceStateKind state,
        ICollection<string>? warnings)
    {
        try
        {
            using var key = global::Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"{ServicesRootPath}\{serviceName}", writable: false);
            if (key is null)
            {
                warnings?.Add($"Service registry key was not found: {serviceName}");
                return CreateFallbackSnapshot(serviceName, fallbackDisplayName, state);
            }

            return FromRegistryKey(serviceName, fallbackDisplayName, state, key);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            warnings?.Add($"Service registry configuration could not be read for {serviceName}: {ex.Message}");
            return CreateFallbackSnapshot(serviceName, fallbackDisplayName, state);
        }
    }

    public static WindowsServiceSnapshot? ReadExistingServiceSnapshot(
        string serviceName,
        ServiceStateKind state,
        ICollection<string>? warnings)
    {
        try
        {
            using var key = global::Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"{ServicesRootPath}\{serviceName}", writable: false);
            if (key is null)
            {
                return null;
            }

            return FromRegistryKey(serviceName, null, state, key);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            warnings?.Add($"Service registry configuration could not be read for {serviceName}: {ex.Message}");
            return null;
        }
    }

    public static RegistryKey OpenServiceKeyForWriting(string serviceName)
    {
        return global::Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"{ServicesRootPath}\{serviceName}", writable: true)
            ?? throw new InvalidOperationException($"Service registry key could not be opened for writing: {serviceName}");
    }

    public static int ToWindowsStartValue(ServiceStartModeKind startMode)
    {
        return startMode switch
        {
            ServiceStartModeKind.Boot => 0,
            ServiceStartModeKind.System => 1,
            ServiceStartModeKind.Automatic => 2,
            ServiceStartModeKind.Manual => 3,
            ServiceStartModeKind.Disabled => 4,
            _ => throw new InvalidOperationException($"Unsupported service start mode for rollback: {startMode}")
        };
    }

    private static WindowsServiceSnapshot FromRegistryKey(
        string serviceName,
        string? fallbackDisplayName,
        ServiceStateKind state,
        RegistryKey key)
    {
        return new WindowsServiceSnapshot(
            serviceName,
            ReadString(key, "DisplayName") ?? fallbackDisplayName ?? serviceName,
            ToStartMode(ReadDWord(key, "Start")),
            ReadString(key, "ImagePath"),
            ReadString(key, "ObjectName"),
            state,
            ToDelayedAutoStart(ReadDWord(key, "DelayedAutoStart")),
            ReadStringList(key, "DependOnService"),
            ReadString(key, "Description"));
    }

    private static WindowsServiceSnapshot CreateFallbackSnapshot(
        string serviceName,
        string? fallbackDisplayName,
        ServiceStateKind state)
    {
        return new WindowsServiceSnapshot(
            serviceName,
            fallbackDisplayName ?? serviceName,
            ServiceStartModeKind.Unknown,
            null,
            null,
            state,
            null,
            Array.Empty<string>(),
            null);
    }

    private static ServiceStartModeKind ToStartMode(int? value)
    {
        return value switch
        {
            0 => ServiceStartModeKind.Boot,
            1 => ServiceStartModeKind.System,
            2 => ServiceStartModeKind.Automatic,
            3 => ServiceStartModeKind.Manual,
            4 => ServiceStartModeKind.Disabled,
            _ => ServiceStartModeKind.Unknown
        };
    }

    private static bool? ToDelayedAutoStart(int? value)
    {
        return value.HasValue ? value.Value != 0 : false;
    }

    private static int? ReadDWord(RegistryKey key, string valueName)
    {
        var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value switch
        {
            int integer => integer,
            long longValue => checked((int)longValue),
            _ => null
        };
    }

    private static string? ReadString(RegistryKey key, string valueName)
    {
        return key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            _ => null
        };
    }

    private static IReadOnlyList<string> ReadStringList(RegistryKey key, string valueName)
    {
        var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        var values = value switch
        {
            string[] array => array,
            string text when !string.IsNullOrWhiteSpace(text) => [text],
            _ => Array.Empty<string>()
        };

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
