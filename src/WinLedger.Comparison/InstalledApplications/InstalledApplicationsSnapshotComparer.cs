using WinLedger.Domain.InstalledApplications;

namespace WinLedger.Comparison.InstalledApplications;

public sealed class InstalledApplicationsSnapshotComparer
{
    public InstalledApplicationsComparison Compare(
        InstalledApplicationsSnapshot baseline,
        InstalledApplicationsSnapshot comparison,
        DateTimeOffset comparedAt)
    {
        if (baseline.SessionId != comparison.SessionId)
        {
            throw new ArgumentException("Installed application snapshots belong to different sessions.", nameof(comparison));
        }

        var changes = new List<InstalledApplicationChange>();
        var warnings = baseline.Warnings.Concat(comparison.Warnings).ToList();
        var baselineApplications = CreateApplicationMap(baseline.Applications, "baseline", warnings);
        var comparisonApplications = CreateApplicationMap(comparison.Applications, "comparison", warnings);

        foreach (var (identity, after) in comparisonApplications)
        {
            if (!baselineApplications.TryGetValue(identity, out var before))
            {
                changes.Add(CreateChange(InstalledApplicationChangeKind.ApplicationRegistered, null, after));
                continue;
            }

            AddApplicationPropertyChanges(before, after, changes);
        }

        foreach (var (identity, before) in baselineApplications)
        {
            if (!comparisonApplications.ContainsKey(identity))
            {
                changes.Add(CreateChange(InstalledApplicationChangeKind.ApplicationRemoved, before, null));
            }
        }

        return new InstalledApplicationsComparison(
            Guid.NewGuid(),
            baseline.SessionId,
            baseline.Id,
            comparison.Id,
            comparedAt,
            changes.OrderBy(change => change.TargetDisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(change => change.Kind).ToArray(),
            warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void AddApplicationPropertyChanges(
        InstalledApplicationSnapshot before,
        InstalledApplicationSnapshot after,
        List<InstalledApplicationChange> changes)
    {
        AddStringChange(before.DisplayName, after.DisplayName, InstalledApplicationChangeKind.DisplayNameChanged, before, after, changes);
        AddStringChange(before.DisplayVersion, after.DisplayVersion, InstalledApplicationChangeKind.VersionChanged, before, after, changes);
        AddStringChange(before.Publisher, after.Publisher, InstalledApplicationChangeKind.PublisherChanged, before, after, changes);
        AddStringChange(before.InstallLocation, after.InstallLocation, InstalledApplicationChangeKind.InstallLocationChanged, before, after, changes);
        AddStringChange(before.InstallSource, after.InstallSource, InstalledApplicationChangeKind.InstallSourceChanged, before, after, changes);
        AddStringChange(before.InstallDate, after.InstallDate, InstalledApplicationChangeKind.InstallDateChanged, before, after, changes);
        AddStringChange(before.UninstallString, after.UninstallString, InstalledApplicationChangeKind.UninstallCommandChanged, before, after, changes);
        AddStringChange(before.QuietUninstallString, after.QuietUninstallString, InstalledApplicationChangeKind.QuietUninstallCommandChanged, before, after, changes);
        AddStringChange(before.ModifyPath, after.ModifyPath, InstalledApplicationChangeKind.ModifyPathChanged, before, after, changes);
        AddStringChange(before.ReleaseType, after.ReleaseType, InstalledApplicationChangeKind.ReleaseTypeChanged, before, after, changes);
        AddStringChange(before.Comments, after.Comments, InstalledApplicationChangeKind.CommentsChanged, before, after, changes);
        AddStringChange(before.UrlInfoAbout, after.UrlInfoAbout, InstalledApplicationChangeKind.UrlInfoChanged, before, after, changes);
        AddStringChange(before.PackageFullName, after.PackageFullName, InstalledApplicationChangeKind.PackageFullNameChanged, before, after, changes);
        AddStringChange(before.PackageFamilyName, after.PackageFamilyName, InstalledApplicationChangeKind.PackageFamilyNameChanged, before, after, changes);
        AddStringChange(before.PackageName, after.PackageName, InstalledApplicationChangeKind.PackageNameChanged, before, after, changes);
        AddStringChange(before.PackagePublisherId, after.PackagePublisherId, InstalledApplicationChangeKind.PackagePublisherIdChanged, before, after, changes);
        AddStringChange(before.PackageResourceId, after.PackageResourceId, InstalledApplicationChangeKind.PackageResourceIdChanged, before, after, changes);
        AddStringChange(before.PackageManifestPath, after.PackageManifestPath, InstalledApplicationChangeKind.PackageManifestPathChanged, before, after, changes);

        if (before.EstimatedSizeKb != after.EstimatedSizeKb)
        {
            changes.Add(CreateChange(InstalledApplicationChangeKind.EstimatedSizeChanged, before, after));
        }

        if (before.WindowsInstaller != after.WindowsInstaller)
        {
            changes.Add(CreateChange(InstalledApplicationChangeKind.WindowsInstallerChanged, before, after));
        }

        if (before.SystemComponent != after.SystemComponent)
        {
            changes.Add(CreateChange(InstalledApplicationChangeKind.SystemComponentChanged, before, after));
        }

        if (before.AppxInboxPackage != after.AppxInboxPackage)
        {
            changes.Add(CreateChange(InstalledApplicationChangeKind.AppxInboxPackageChanged, before, after));
        }
    }

    private static void AddStringChange(
        string? beforeValue,
        string? afterValue,
        InstalledApplicationChangeKind kind,
        InstalledApplicationSnapshot before,
        InstalledApplicationSnapshot after,
        List<InstalledApplicationChange> changes)
    {
        if (!string.Equals(beforeValue, afterValue, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(CreateChange(kind, before, after));
        }
    }

    private static InstalledApplicationChange CreateChange(
        InstalledApplicationChangeKind kind,
        InstalledApplicationSnapshot? before,
        InstalledApplicationSnapshot? after)
    {
        var availability = InstalledApplicationChangeExplainer.GetRollbackAvailability(kind, before, after);
        var applicationName = after?.DisplayName ?? before?.DisplayName ?? "(unknown)";

        return new InstalledApplicationChange(
            Guid.NewGuid(),
            kind,
            applicationName,
            before,
            after,
            InstalledApplicationChangeExplainer.Summarize(kind, before, after),
            InstalledApplicationChangeExplainer.Classify(kind, before, after, availability),
            availability);
    }

    private static Dictionary<string, InstalledApplicationSnapshot> CreateApplicationMap(
        IReadOnlyList<InstalledApplicationSnapshot> applications,
        string snapshotRole,
        List<string> warnings)
    {
        var map = new Dictionary<string, InstalledApplicationSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var application in applications.OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            if (!map.TryAdd(application.Identity, application))
            {
                warnings.Add($"Duplicate installed application identity ignored in {snapshotRole} snapshot: {application.Identity}");
            }
        }

        return map;
    }
}
