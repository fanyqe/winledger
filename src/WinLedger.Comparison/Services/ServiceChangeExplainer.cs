using WinLedger.Domain.Rollback;
using WinLedger.Domain.Services;

namespace WinLedger.Comparison.Services;

public static class ServiceChangeExplainer
{
    public static string Summarize(ServiceChangeKind kind, WindowsServiceSnapshot? before, WindowsServiceSnapshot? after)
    {
        var serviceName = after?.Name ?? before?.Name ?? "unknown service";
        var displayName = after?.DisplayName ?? before?.DisplayName ?? serviceName;

        return kind switch
        {
            ServiceChangeKind.ServiceCreated => $"The service \"{displayName}\" was created and configured to start as {after?.StartMode}.",
            ServiceChangeKind.ServiceRemoved => $"The service \"{displayName}\" was removed.",
            ServiceChangeKind.StartModeChanged => $"The service \"{displayName}\" start mode changed from {before?.StartMode} to {after?.StartMode}.",
            ServiceChangeKind.ExecutablePathChanged => $"The service \"{displayName}\" executable path changed from {Display(before?.ExecutablePath)} to {Display(after?.ExecutablePath)}.",
            ServiceChangeKind.DisplayNameChanged => $"The service {serviceName} display name changed from {Display(before?.DisplayName)} to {Display(after?.DisplayName)}.",
            ServiceChangeKind.ServiceAccountChanged => $"The service \"{displayName}\" account changed from {Display(before?.ServiceAccount)} to {Display(after?.ServiceAccount)}.",
            ServiceChangeKind.StateChanged => $"The service \"{displayName}\" state changed from {before?.State} to {after?.State}.",
            ServiceChangeKind.DelayedAutoStartChanged => $"The service \"{displayName}\" delayed automatic start changed from {Display(before?.DelayedAutoStart)} to {Display(after?.DelayedAutoStart)}.",
            ServiceChangeKind.DependenciesChanged => $"The service \"{displayName}\" dependencies changed from {Display(before?.Dependencies)} to {Display(after?.Dependencies)}.",
            _ => $"The service \"{displayName}\" changed."
        };
    }

    public static IReadOnlySet<ChangeAttentionLabel> Classify(
        ServiceChangeKind kind,
        WindowsServiceSnapshot? before,
        WindowsServiceSnapshot? after,
        RollbackAvailability rollbackAvailability)
    {
        var labels = new HashSet<ChangeAttentionLabel>
        {
            ChangeAttentionLabel.Persistent,
            ChangeAttentionLabel.Privileged
        };

        if (kind is ServiceChangeKind.ServiceCreated or ServiceChangeKind.StartModeChanged or ServiceChangeKind.DelayedAutoStartChanged ||
            after?.StartMode is ServiceStartModeKind.Automatic or ServiceStartModeKind.Boot or ServiceStartModeKind.System ||
            before?.StartMode is ServiceStartModeKind.Automatic or ServiceStartModeKind.Boot or ServiceStartModeKind.System)
        {
            labels.Add(ChangeAttentionLabel.StartupRelated);
        }

        if (kind is ServiceChangeKind.ServiceRemoved or ServiceChangeKind.ExecutablePathChanged or ServiceChangeKind.ServiceAccountChanged or ServiceChangeKind.DependenciesChanged)
        {
            labels.Add(ChangeAttentionLabel.PotentiallyDestructive);
        }

        if (IsSensitiveAccount(before?.ServiceAccount) || IsSensitiveAccount(after?.ServiceAccount))
        {
            labels.Add(ChangeAttentionLabel.SecuritySensitive);
        }

        if (rollbackAvailability is RollbackAvailability.Unavailable or RollbackAvailability.ManualReview)
        {
            labels.Add(ChangeAttentionLabel.RollbackUnavailable);
        }

        return labels;
    }

    public static RollbackAvailability GetRollbackAvailability(ServiceChangeKind kind)
    {
        return kind switch
        {
            ServiceChangeKind.StartModeChanged or ServiceChangeKind.DelayedAutoStartChanged => RollbackAvailability.RequiresConfirmation,
            ServiceChangeKind.StateChanged => RollbackAvailability.Unavailable,
            ServiceChangeKind.ServiceRemoved => RollbackAvailability.Unavailable,
            _ => RollbackAvailability.ManualReview
        };
    }

    private static bool IsSensitiveAccount(string? serviceAccount)
    {
        if (string.IsNullOrWhiteSpace(serviceAccount))
        {
            return false;
        }

        return serviceAccount.Contains("LocalSystem", StringComparison.OrdinalIgnoreCase) ||
               serviceAccount.Contains("NetworkService", StringComparison.OrdinalIgnoreCase) ||
               serviceAccount.Contains("LocalService", StringComparison.OrdinalIgnoreCase);
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(empty)" : $"\"{value}\"";
    }

    private static string Display(bool? value)
    {
        return value.HasValue ? value.Value.ToString() : "Unknown";
    }

    private static string Display(IReadOnlyList<string>? values)
    {
        return values is null || values.Count == 0
            ? "(none)"
            : string.Join(", ", values);
    }
}
