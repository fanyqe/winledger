using WinLedger.Domain.Registry;
using WinLedger.Domain.Rollback;

namespace WinLedger.Comparison.Registry;

public static class RegistryChangeExplainer
{
    public static string SummarizeKeyChange(RegistryChangeKind kind, RegistryPath path)
    {
        return kind switch
        {
            RegistryChangeKind.KeyCreated => $"The registry key {path} was created.",
            RegistryChangeKind.KeyRemoved => $"The registry key {path} was removed.",
            _ => $"The registry key {path} changed."
        };
    }

    public static string SummarizeValueChange(
        RegistryChangeKind kind,
        RegistryPath path,
        string valueName,
        RegistryValueSnapshot? before,
        RegistryValueSnapshot? after)
    {
        var displayName = string.IsNullOrEmpty(valueName) ? "(Default)" : valueName;

        return kind switch
        {
            RegistryChangeKind.ValueCreated => $"The registry value \"{displayName}\" was added under {path}.",
            RegistryChangeKind.ValueRemoved => $"The registry value \"{displayName}\" was removed from {path}.",
            RegistryChangeKind.ValueModified => $"The registry value \"{displayName}\" under {path} changed from {before?.DisplayValue} to {after?.DisplayValue}.",
            RegistryChangeKind.ValueTypeChanged => $"The registry value \"{displayName}\" under {path} changed type from {before?.ValueType} to {after?.ValueType}.",
            _ => $"The registry value \"{displayName}\" under {path} changed."
        };
    }

    public static IReadOnlySet<ChangeAttentionLabel> Classify(
        RegistryPath path,
        string? valueName,
        RollbackAvailability rollbackAvailability)
    {
        var labels = new HashSet<ChangeAttentionLabel> { ChangeAttentionLabel.Persistent };
        var fullPath = path.FullPath.ToUpperInvariant();
        var normalizedValueName = valueName?.ToUpperInvariant() ?? string.Empty;

        if (fullPath.Contains("\\CURRENTVERSION\\RUN", StringComparison.Ordinal) ||
            fullPath.Contains("\\CURRENTVERSION\\RUNONCE", StringComparison.Ordinal) ||
            fullPath.Contains("\\SERVICES\\", StringComparison.Ordinal))
        {
            labels.Add(ChangeAttentionLabel.StartupRelated);
        }

        if (path.Hive == RegistryHiveKind.LocalMachine ||
            fullPath.Contains("\\SERVICES\\", StringComparison.Ordinal))
        {
            labels.Add(ChangeAttentionLabel.Privileged);
        }

        if (fullPath.Contains("\\POLICIES\\", StringComparison.Ordinal) ||
            fullPath.Contains("WINDOWS DEFENDER", StringComparison.Ordinal) ||
            normalizedValueName.Contains("DISABLE", StringComparison.Ordinal))
        {
            labels.Add(ChangeAttentionLabel.SecuritySensitive);
        }

        if (fullPath.Contains("\\TCPIP\\", StringComparison.Ordinal) ||
            fullPath.Contains("\\INTERNET SETTINGS", StringComparison.Ordinal))
        {
            labels.Add(ChangeAttentionLabel.NetworkRelated);
        }

        if (rollbackAvailability is RollbackAvailability.Unavailable or RollbackAvailability.ManualReview)
        {
            labels.Add(ChangeAttentionLabel.RollbackUnavailable);
        }

        return labels;
    }
}
