using WinLedger.Domain.InstalledApplications;

namespace WinLedger.Tests;

internal static class InstalledApplicationTestData
{
    public static InstalledApplicationsSnapshot Snapshot(
        Guid sessionId,
        params InstalledApplicationSnapshot[] applications)
    {
        return new InstalledApplicationsSnapshot(
            Guid.NewGuid(),
            sessionId,
            "Installed applications",
            DateTimeOffset.UtcNow,
            applications,
            []);
    }

    public static InstalledApplicationSnapshot Application(
        string displayName,
        string keyName = "ExampleApp",
        InstalledApplicationScopeKind scope = InstalledApplicationScopeKind.Machine,
        InstalledApplicationArchitectureKind architecture = InstalledApplicationArchitectureKind.X64,
        InstalledApplicationSourceKind source = InstalledApplicationSourceKind.RegistryUninstall,
        string? displayVersion = "1.0.0",
        string? publisher = "Example Publisher",
        string? installLocation = @"C:\Program Files\Example",
        string? uninstallString = @"""C:\Program Files\Example\uninstall.exe""",
        bool windowsInstaller = false,
        bool systemComponent = false)
    {
        var registryKeyPath = $@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{keyName}";
        return new InstalledApplicationSnapshot(
            $"HKLM|Registry64|SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{keyName}",
            scope,
            architecture,
            source,
            registryKeyPath,
            keyName,
            source == InstalledApplicationSourceKind.MsiProduct ? keyName : null,
            displayName,
            displayVersion,
            publisher,
            installLocation,
            @"C:\Installers",
            "20260723",
            uninstallString,
            @"""C:\Program Files\Example\uninstall.exe"" /quiet",
            @"""C:\Program Files\Example\setup.exe"" /modify",
            1024,
            windowsInstaller,
            systemComponent,
            null,
            "Example comments",
            "https://example.test");
    }

    public static InstalledApplicationSnapshot AppxPackage(
        string displayName,
        string packageFullName = "Example.Package_1.0.0.0_x64__publisherid",
        string packageName = "Example.Package",
        string packageVersion = "1.0.0.0",
        string packageFamilyName = "Example.Package_publisherid",
        string publisherId = "publisherid",
        string resourceId = "",
        InstalledApplicationScopeKind scope = InstalledApplicationScopeKind.User,
        InstalledApplicationArchitectureKind architecture = InstalledApplicationArchitectureKind.X64,
        bool inboxPackage = false)
    {
        return new InstalledApplicationSnapshot(
            $"APPX|UserRepository|{packageName}|x64|{resourceId}|{publisherId}",
            scope,
            architecture,
            InstalledApplicationSourceKind.AppxPackage,
            $@"HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages\{packageFullName}",
            packageFullName,
            null,
            displayName,
            packageVersion,
            null,
            $@"C:\Program Files\WindowsApps\{packageFullName}",
            "UserRepository",
            null,
            null,
            null,
            null,
            null,
            false,
            inboxPackage,
            null,
            null,
            null,
            packageFullName,
            packageFamilyName,
            packageName,
            publisherId,
            resourceId,
            $@"C:\Program Files\WindowsApps\{packageFullName}\AppxManifest.xml",
            inboxPackage);
    }
}
