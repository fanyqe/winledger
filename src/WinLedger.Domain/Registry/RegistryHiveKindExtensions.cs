namespace WinLedger.Domain.Registry;

public static class RegistryHiveKindExtensions
{
    public static string ToRootName(this RegistryHiveKind hive)
    {
        return hive switch
        {
            RegistryHiveKind.CurrentUser => "HKEY_CURRENT_USER",
            RegistryHiveKind.LocalMachine => "HKEY_LOCAL_MACHINE",
            RegistryHiveKind.ClassesRoot => "HKEY_CLASSES_ROOT",
            RegistryHiveKind.Users => "HKEY_USERS",
            RegistryHiveKind.CurrentConfig => "HKEY_CURRENT_CONFIG",
            _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, "Unsupported registry hive.")
        };
    }

    public static RegistryHiveKind ToRegistryHiveKind(this string hive)
    {
        var normalized = hive.Trim().ToUpperInvariant();
        return normalized switch
        {
            "HKCU" or "HKEY_CURRENT_USER" => RegistryHiveKind.CurrentUser,
            "HKLM" or "HKEY_LOCAL_MACHINE" => RegistryHiveKind.LocalMachine,
            "HKCR" or "HKEY_CLASSES_ROOT" => RegistryHiveKind.ClassesRoot,
            "HKU" or "HKEY_USERS" => RegistryHiveKind.Users,
            "HKCC" or "HKEY_CURRENT_CONFIG" => RegistryHiveKind.CurrentConfig,
            _ => throw new ArgumentException($"Unsupported registry hive '{hive}'.", nameof(hive))
        };
    }
}
