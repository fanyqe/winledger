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

        var parsed = ExtractViewSuffix(value, view);
        var normalized = parsed.Path.Replace('/', '\\').Trim();
        var parts = normalized.Split('\\', 2, StringSplitOptions.TrimEntries);
        var hive = parts[0].ToRegistryHiveKind();
        var keyPath = parts.Length == 1 ? string.Empty : parts[1];

        return new RegistryPath(hive, keyPath, parsed.View);
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

    private static (string Path, RegistryViewKind View) ExtractViewSuffix(
        string value,
        RegistryViewKind defaultView)
    {
        var trimmed = value.Trim();
        if (!trimmed.EndsWith(']'))
        {
            return (trimmed, defaultView);
        }

        var marker = trimmed.LastIndexOf(" [", StringComparison.Ordinal);
        if (marker < 0)
        {
            return (trimmed, defaultView);
        }

        var viewName = trimmed[(marker + 2)..^1].Trim();
        return (trimmed[..marker].Trim(), ParseViewKind(viewName));
    }

    private static RegistryViewKind ParseViewKind(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "DEFAULT" => RegistryViewKind.Default,
            "REGISTRY32" or "32" or "X86" => RegistryViewKind.Registry32,
            "REGISTRY64" or "64" or "X64" => RegistryViewKind.Registry64,
            _ => throw new ArgumentException($"Unsupported registry view '{value}'.", nameof(value))
        };
    }
}
