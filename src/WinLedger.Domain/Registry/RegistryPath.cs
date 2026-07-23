namespace WinLedger.Domain.Registry;

public sealed record RegistryPath
{
    public RegistryPath(RegistryHiveKind hive, string keyPath, RegistryViewKind view = RegistryViewKind.Default)
    {
        Hive = hive;
        KeyPath = NormalizeKeyPath(keyPath);
        View = view;
    }

    public RegistryHiveKind Hive { get; }

    public string KeyPath { get; }

    public RegistryViewKind View { get; }

    public string FullPath => string.IsNullOrWhiteSpace(KeyPath)
        ? Hive.ToRootName()
        : $"{Hive.ToRootName()}\\{KeyPath}";

    public static RegistryPath Parse(string value, RegistryViewKind view = RegistryViewKind.Default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Replace('/', '\\').Trim();
        var parts = normalized.Split('\\', 2, StringSplitOptions.TrimEntries);
        var hive = parts[0].ToRegistryHiveKind();
        var keyPath = parts.Length == 1 ? string.Empty : parts[1];

        return new RegistryPath(hive, keyPath, view);
    }

    public override string ToString()
    {
        return View == RegistryViewKind.Default ? FullPath : $"{FullPath} [{View}]";
    }

    private static string NormalizeKeyPath(string keyPath)
    {
        ArgumentNullException.ThrowIfNull(keyPath);
        return keyPath.Replace('/', '\\').Trim().Trim('\\');
    }
}
