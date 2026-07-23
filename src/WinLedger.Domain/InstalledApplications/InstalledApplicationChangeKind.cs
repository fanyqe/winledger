namespace WinLedger.Domain.InstalledApplications;

public enum InstalledApplicationChangeKind
{
    ApplicationRegistered,
    ApplicationRemoved,
    DisplayNameChanged,
    VersionChanged,
    PublisherChanged,
    InstallLocationChanged,
    InstallSourceChanged,
    InstallDateChanged,
    UninstallCommandChanged,
    QuietUninstallCommandChanged,
    ModifyPathChanged,
    EstimatedSizeChanged,
    WindowsInstallerChanged,
    SystemComponentChanged,
    ReleaseTypeChanged,
    CommentsChanged,
    UrlInfoChanged,
    PackageFullNameChanged,
    PackageFamilyNameChanged,
    PackageNameChanged,
    PackagePublisherIdChanged,
    PackageResourceIdChanged,
    PackageManifestPathChanged,
    AppxInboxPackageChanged
}
