using WinLedger.Domain.InstalledApplications;
using WinLedger.Domain.Rollback;

namespace WinLedger.Comparison.InstalledApplications;

public static class InstalledApplicationChangeExplainer
{
    public static string Summarize(
        InstalledApplicationChangeKind kind,
        InstalledApplicationSnapshot? before,
        InstalledApplicationSnapshot? after)
    {
        var application = after ?? before;
        var name = application?.DisplayName ?? "unknown application";
        var subject = GetSubject(application);

        return kind switch
        {
            InstalledApplicationChangeKind.ApplicationRegistered => $"The {subject} \"{name}\" was registered.",
            InstalledApplicationChangeKind.ApplicationRemoved => $"The {subject} \"{name}\" was removed from registration.",
            InstalledApplicationChangeKind.DisplayNameChanged => $"The {subject} display name changed from {Display(before?.DisplayName)} to {Display(after?.DisplayName)}.",
            InstalledApplicationChangeKind.VersionChanged => $"The {subject} \"{name}\" version changed from {Display(before?.DisplayVersion)} to {Display(after?.DisplayVersion)}.",
            InstalledApplicationChangeKind.PublisherChanged => $"The {subject} \"{name}\" publisher changed from {Display(before?.Publisher)} to {Display(after?.Publisher)}.",
            InstalledApplicationChangeKind.InstallLocationChanged => $"The {subject} \"{name}\" install location changed from {Display(before?.InstallLocation)} to {Display(after?.InstallLocation)}.",
            InstalledApplicationChangeKind.InstallSourceChanged => $"The {subject} \"{name}\" install source changed from {Display(before?.InstallSource)} to {Display(after?.InstallSource)}.",
            InstalledApplicationChangeKind.InstallDateChanged => $"The {subject} \"{name}\" install date changed from {Display(before?.InstallDate)} to {Display(after?.InstallDate)}.",
            InstalledApplicationChangeKind.UninstallCommandChanged => $"The {subject} \"{name}\" uninstall command changed.",
            InstalledApplicationChangeKind.QuietUninstallCommandChanged => $"The {subject} \"{name}\" quiet uninstall command changed.",
            InstalledApplicationChangeKind.ModifyPathChanged => $"The {subject} \"{name}\" modify command changed.",
            InstalledApplicationChangeKind.EstimatedSizeChanged => $"The {subject} \"{name}\" estimated size changed from {DisplaySize(before?.EstimatedSizeKb)} to {DisplaySize(after?.EstimatedSizeKb)}.",
            InstalledApplicationChangeKind.WindowsInstallerChanged => $"The {subject} \"{name}\" Windows Installer marker changed.",
            InstalledApplicationChangeKind.SystemComponentChanged => $"The {subject} \"{name}\" system component marker changed.",
            InstalledApplicationChangeKind.ReleaseTypeChanged => $"The {subject} \"{name}\" release type changed from {Display(before?.ReleaseType)} to {Display(after?.ReleaseType)}.",
            InstalledApplicationChangeKind.CommentsChanged => $"The {subject} \"{name}\" comments changed.",
            InstalledApplicationChangeKind.UrlInfoChanged => $"The {subject} \"{name}\" information URL changed from {Display(before?.UrlInfoAbout)} to {Display(after?.UrlInfoAbout)}.",
            InstalledApplicationChangeKind.PackageFullNameChanged => $"The {subject} \"{name}\" full name changed from {Display(before?.PackageFullName)} to {Display(after?.PackageFullName)}.",
            InstalledApplicationChangeKind.PackageFamilyNameChanged => $"The {subject} \"{name}\" family name changed from {Display(before?.PackageFamilyName)} to {Display(after?.PackageFamilyName)}.",
            InstalledApplicationChangeKind.PackageNameChanged => $"The {subject} \"{name}\" name changed from {Display(before?.PackageName)} to {Display(after?.PackageName)}.",
            InstalledApplicationChangeKind.PackagePublisherIdChanged => $"The {subject} \"{name}\" publisher identity changed from {Display(before?.PackagePublisherId)} to {Display(after?.PackagePublisherId)}.",
            InstalledApplicationChangeKind.PackageResourceIdChanged => $"The {subject} \"{name}\" resource identity changed from {Display(before?.PackageResourceId)} to {Display(after?.PackageResourceId)}.",
            InstalledApplicationChangeKind.PackageManifestPathChanged => $"The {subject} \"{name}\" manifest path changed from {Display(before?.PackageManifestPath)} to {Display(after?.PackageManifestPath)}.",
            InstalledApplicationChangeKind.AppxInboxPackageChanged => $"The {subject} \"{name}\" inbox application marker changed.",
            _ => $"The {subject} \"{name}\" changed."
        };
    }

    public static IReadOnlySet<ChangeAttentionLabel> Classify(
        InstalledApplicationChangeKind kind,
        InstalledApplicationSnapshot? before,
        InstalledApplicationSnapshot? after,
        RollbackAvailability rollbackAvailability)
    {
        var labels = new HashSet<ChangeAttentionLabel>
        {
            ChangeAttentionLabel.Persistent
        };

        var application = after ?? before;
        if (application?.Scope == InstalledApplicationScopeKind.Machine)
        {
            labels.Add(ChangeAttentionLabel.Privileged);
        }

        if (kind is InstalledApplicationChangeKind.ApplicationRemoved or
            InstalledApplicationChangeKind.UninstallCommandChanged or
            InstalledApplicationChangeKind.QuietUninstallCommandChanged or
            InstalledApplicationChangeKind.ModifyPathChanged or
            InstalledApplicationChangeKind.SystemComponentChanged or
            InstalledApplicationChangeKind.AppxInboxPackageChanged)
        {
            labels.Add(ChangeAttentionLabel.PotentiallyDestructive);
        }

        if (rollbackAvailability is RollbackAvailability.Unavailable or RollbackAvailability.ManualReview)
        {
            labels.Add(ChangeAttentionLabel.RollbackUnavailable);
        }

        if (labels.Count == 0)
        {
            labels.Add(ChangeAttentionLabel.Informational);
        }

        return labels;
    }

    public static RollbackAvailability GetRollbackAvailability(
        InstalledApplicationChangeKind kind,
        InstalledApplicationSnapshot? before,
        InstalledApplicationSnapshot? after)
    {
        return RollbackAvailability.ManualReview;
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(empty)" : $"\"{value}\"";
    }

    private static string DisplaySize(long? estimatedSizeKb)
    {
        return estimatedSizeKb is null ? "(empty)" : $"{estimatedSizeKb.Value} KB";
    }

    private static string GetSubject(InstalledApplicationSnapshot? application)
    {
        return application?.Source == InstalledApplicationSourceKind.AppxPackage
            ? "AppX/MSIX package"
            : "installed application";
    }
}
