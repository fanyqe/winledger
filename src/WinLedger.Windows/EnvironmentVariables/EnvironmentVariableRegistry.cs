using Microsoft.Win32;
using WinLedger.Domain.EnvironmentVariables;

namespace WinLedger.Windows.EnvironmentVariables;

internal static class EnvironmentVariableRegistry
{
    private const string UserEnvironmentKeyPath = "Environment";
    private const string MachineEnvironmentKeyPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";

    public static IReadOnlyList<EnvironmentVariableSnapshot> ReadScope(
        EnvironmentVariableScopeKind scope,
        List<string> warnings)
    {
        var variables = new List<EnvironmentVariableSnapshot>();
        var location = Location(scope);

        try
        {
            using var root = RegistryKey.OpenBaseKey(location.Hive, location.View);
            using var key = root.OpenSubKey(location.KeyPath, writable: false);
            if (key is null)
            {
                warnings.Add($"Environment variable key was not found: {location.SourceKey}");
                return variables;
            }

            foreach (var valueName in key.GetValueNames().Order(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    variables.Add(ReadValue(location, key, valueName));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    warnings.Add($"Environment variable could not be read: {location.SourceKey}\\{valueName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            warnings.Add($"Environment variable collection failed for {location.SourceKey}: {ex.Message}");
        }

        return variables;
    }

    public static EnvironmentVariableSnapshot? ReadVariable(EnvironmentVariableScopeKind scope, string name)
    {
        var location = Location(scope);
        using var root = RegistryKey.OpenBaseKey(location.Hive, location.View);
        using var key = root.OpenSubKey(location.KeyPath, writable: false);
        if (key is null)
        {
            return null;
        }

        var actualName = key.GetValueNames()
            .FirstOrDefault(valueName => string.Equals(valueName, name, StringComparison.OrdinalIgnoreCase));
        if (actualName is null)
        {
            return null;
        }

        return ReadValue(location, key, actualName);
    }

    public static void SetVariable(EnvironmentVariableSnapshot variable)
    {
        var location = Location(variable.Scope);
        using var root = RegistryKey.OpenBaseKey(location.Hive, location.View);
        using var key = root.CreateSubKey(location.KeyPath, writable: true)
            ?? throw new InvalidOperationException($"Environment variable key could not be opened for writing: {location.SourceKey}");

        key.SetValue(variable.Name, variable.RawValue, ToWindowsValueKind(variable.ValueType));
    }

    public static void DeleteVariable(EnvironmentVariableScopeKind scope, string name)
    {
        var location = Location(scope);
        using var root = RegistryKey.OpenBaseKey(location.Hive, location.View);
        using var key = root.OpenSubKey(location.KeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    private static EnvironmentVariableSnapshot ReadValue(EnvironmentRegistryLocation location, RegistryKey key, string valueName)
    {
        var kind = key.GetValueKind(valueName);
        var rawValue = key.GetValue(valueName, string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? string.Empty;
        return new EnvironmentVariableSnapshot(
            location.Scope,
            valueName,
            rawValue,
            ToDomainValueType(kind),
            IsPathVariable(valueName) ? SplitPath(rawValue) : Array.Empty<string>(),
            location.SourceKey);
    }

    private static IReadOnlyList<string> SplitPath(string rawValue)
    {
        return rawValue
            .Split(';', StringSplitOptions.None)
            .Select(entry => entry.Trim())
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .ToArray();
    }

    private static bool IsPathVariable(string name)
    {
        return string.Equals(name, "Path", StringComparison.OrdinalIgnoreCase);
    }

    private static EnvironmentVariableValueType ToDomainValueType(RegistryValueKind kind)
    {
        return kind switch
        {
            RegistryValueKind.String => EnvironmentVariableValueType.String,
            RegistryValueKind.ExpandString => EnvironmentVariableValueType.ExpandString,
            _ => EnvironmentVariableValueType.Unknown
        };
    }

    private static RegistryValueKind ToWindowsValueKind(EnvironmentVariableValueType type)
    {
        return type switch
        {
            EnvironmentVariableValueType.String => RegistryValueKind.String,
            EnvironmentVariableValueType.ExpandString => RegistryValueKind.ExpandString,
            _ => throw new InvalidOperationException($"Unsupported environment variable value type: {type}")
        };
    }

    private static EnvironmentRegistryLocation Location(EnvironmentVariableScopeKind scope)
    {
        return scope switch
        {
            EnvironmentVariableScopeKind.User => new EnvironmentRegistryLocation(
                EnvironmentVariableScopeKind.User,
                RegistryHive.CurrentUser,
                RegistryView.Default,
                UserEnvironmentKeyPath,
                @"HKCU\Environment"),
            EnvironmentVariableScopeKind.Machine => new EnvironmentRegistryLocation(
                EnvironmentVariableScopeKind.Machine,
                RegistryHive.LocalMachine,
                RegistryView.Registry64,
                MachineEnvironmentKeyPath,
                @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported environment variable scope.")
        };
    }

    private sealed record EnvironmentRegistryLocation(
        EnvironmentVariableScopeKind Scope,
        RegistryHive Hive,
        RegistryView View,
        string KeyPath,
        string SourceKey);
}
