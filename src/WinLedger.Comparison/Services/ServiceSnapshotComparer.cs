using WinLedger.Domain.Services;

namespace WinLedger.Comparison.Services;

public sealed class ServiceSnapshotComparer
{
    public ServiceComparison Compare(ServiceSnapshot baseline, ServiceSnapshot comparison, DateTimeOffset comparedAt)
    {
        if (baseline.SessionId != comparison.SessionId)
        {
            throw new ArgumentException("Service snapshots belong to different sessions.", nameof(comparison));
        }

        var changes = new List<ServiceChange>();
        var baselineServices = baseline.Services.ToDictionary(service => service.Name, StringComparer.OrdinalIgnoreCase);
        var comparisonServices = comparison.Services.ToDictionary(service => service.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var (serviceName, after) in comparisonServices)
        {
            if (!baselineServices.TryGetValue(serviceName, out var before))
            {
                changes.Add(CreateChange(ServiceChangeKind.ServiceCreated, serviceName, null, after));
                continue;
            }

            AddServicePropertyChanges(serviceName, before, after, changes);
        }

        foreach (var (serviceName, before) in baselineServices)
        {
            if (!comparisonServices.ContainsKey(serviceName))
            {
                changes.Add(CreateChange(ServiceChangeKind.ServiceRemoved, serviceName, before, null));
            }
        }

        return new ServiceComparison(
            Guid.NewGuid(),
            baseline.SessionId,
            baseline.Id,
            comparison.Id,
            comparedAt,
            changes.OrderBy(change => change.TargetDisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(change => change.Kind).ToArray(),
            baseline.Warnings.Concat(comparison.Warnings).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void AddServicePropertyChanges(
        string serviceName,
        WindowsServiceSnapshot before,
        WindowsServiceSnapshot after,
        List<ServiceChange> changes)
    {
        if (before.StartMode != after.StartMode)
        {
            changes.Add(CreateChange(ServiceChangeKind.StartModeChanged, serviceName, before, after));
        }

        if (!string.Equals(before.ExecutablePath, after.ExecutablePath, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(CreateChange(ServiceChangeKind.ExecutablePathChanged, serviceName, before, after));
        }

        if (!string.Equals(before.DisplayName, after.DisplayName, StringComparison.Ordinal))
        {
            changes.Add(CreateChange(ServiceChangeKind.DisplayNameChanged, serviceName, before, after));
        }

        if (!string.Equals(before.ServiceAccount, after.ServiceAccount, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(CreateChange(ServiceChangeKind.ServiceAccountChanged, serviceName, before, after));
        }

        if (before.State != after.State)
        {
            changes.Add(CreateChange(ServiceChangeKind.StateChanged, serviceName, before, after));
        }

        if (before.DelayedAutoStart != after.DelayedAutoStart)
        {
            changes.Add(CreateChange(ServiceChangeKind.DelayedAutoStartChanged, serviceName, before, after));
        }

        if (!DependenciesMatch(before.Dependencies, after.Dependencies))
        {
            changes.Add(CreateChange(ServiceChangeKind.DependenciesChanged, serviceName, before, after));
        }
    }

    private static ServiceChange CreateChange(
        ServiceChangeKind kind,
        string serviceName,
        WindowsServiceSnapshot? before,
        WindowsServiceSnapshot? after)
    {
        var availability = ServiceChangeExplainer.GetRollbackAvailability(kind);

        return new ServiceChange(
            Guid.NewGuid(),
            kind,
            serviceName,
            before,
            after,
            ServiceChangeExplainer.Summarize(kind, before, after),
            ServiceChangeExplainer.Classify(kind, before, after, availability),
            availability);
    }

    private static bool DependenciesMatch(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        return before.Order(StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(after.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
    }
}
