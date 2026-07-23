using System.Globalization;
using System.IO;
using System.Security;
using Microsoft.Win32;
using WinLedger.Core.Abstractions;
using WinLedger.Core.InstalledApplications;
using WinLedger.Domain.InstalledApplications;

namespace WinLedger.Windows.InstalledApplications;

public sealed class WindowsInstalledApplicationSnapshotCollector(IClock clock) : IInstalledApplicationSnapshotCollector
{
    private const string UninstallSubKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string AppxUserRepositoryPackagesSubKeyPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
    private const string AppxAllUserStoreApplicationsSubKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications";
    private const string AppxAllUserStoreInboxApplicationsSubKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\InboxApplications";

    private static readonly RegistryUninstallLocation[] Locations =
    [
        new(
            RegistryHive.LocalMachine,
            "HKLM",
            RegistryView.Registry64,
            InstalledApplicationScopeKind.Machine,
            InstalledApplicationArchitectureKind.X64),
        new(
            RegistryHive.LocalMachine,
            "HKLM",
            RegistryView.Registry32,
            InstalledApplicationScopeKind.Machine,
            InstalledApplicationArchitectureKind.X86),
        new(
            RegistryHive.CurrentUser,
            "HKCU",
            RegistryView.Registry64,
            InstalledApplicationScopeKind.User,
            InstalledApplicationArchitectureKind.X64),
        new(
            RegistryHive.CurrentUser,
            "HKCU",
            RegistryView.Registry32,
            InstalledApplicationScopeKind.User,
            InstalledApplicationArchitectureKind.X86)
    ];

    private static readonly AppxRegistryLocation[] AppxLocations =
    [
        new(
            RegistryHive.CurrentUser,
            "HKCU",
            RegistryView.Default,
            AppxUserRepositoryPackagesSubKeyPath,
            "UserRepository",
            InstalledApplicationScopeKind.User,
            false),
        new(
            RegistryHive.LocalMachine,
            "HKLM",
            RegistryView.Default,
            AppxAllUserStoreApplicationsSubKeyPath,
            "AllUserStoreApplications",
            InstalledApplicationScopeKind.Machine,
            false),
        new(
            RegistryHive.LocalMachine,
            "HKLM",
            RegistryView.Default,
            AppxAllUserStoreInboxApplicationsSubKeyPath,
            "AllUserStoreInboxApplications",
            InstalledApplicationScopeKind.Machine,
            true)
    ];

    public Task<InstalledApplicationsSnapshot> CaptureAsync(
        Guid sessionId,
        string snapshotName,
        CancellationToken cancellationToken)
    {
        var applications = new List<InstalledApplicationSnapshot>();
        var warnings = new List<string>();

        if (!OperatingSystem.IsWindows())
        {
            warnings.Add("Installed application collection is only available on Windows.");
            return Task.FromResult(CreateSnapshot(sessionId, snapshotName, applications, warnings));
        }

        foreach (var location in Locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadLocation(location, applications, warnings, cancellationToken);
        }

        foreach (var location in AppxLocations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadAppxLocation(location, applications, warnings, cancellationToken);
        }

        return Task.FromResult(CreateSnapshot(sessionId, snapshotName, applications, warnings));
    }

    private InstalledApplicationsSnapshot CreateSnapshot(
        Guid sessionId,
        string snapshotName,
        IReadOnlyList<InstalledApplicationSnapshot> applications,
        IReadOnlyList<string> warnings)
    {
        return new InstalledApplicationsSnapshot(
            Guid.NewGuid(),
            sessionId,
            snapshotName,
            clock.UtcNow,
            applications.OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray(),
            warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void ReadLocation(
        RegistryUninstallLocation location,
        List<InstalledApplicationSnapshot> applications,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
            using var uninstallKey = baseKey.OpenSubKey(UninstallSubKeyPath);
            if (uninstallKey is null)
            {
                return;
            }

            foreach (var keyName in uninstallKey.GetSubKeyNames().Order(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadApplicationKey(location, uninstallKey, keyName, applications, warnings);
            }
        }
        catch (Exception ex) when (IsRegistryReadException(ex))
        {
            warnings.Add($"Installed application registry location could not be read: {location.DisplayName} - {ex.Message}");
        }
    }

    private static void ReadApplicationKey(
        RegistryUninstallLocation location,
        RegistryKey uninstallKey,
        string keyName,
        List<InstalledApplicationSnapshot> applications,
        List<string> warnings)
    {
        var registryKeyPath = $@"{location.HiveName}\{UninstallSubKeyPath}\{keyName}";

        try
        {
            using var applicationKey = uninstallKey.OpenSubKey(keyName);
            if (applicationKey is null)
            {
                warnings.Add($"Installed application registry key disappeared during collection: {registryKeyPath}");
                return;
            }

            var displayName = Normalize(ReadString(applicationKey, "DisplayName"));
            if (displayName is null)
            {
                return;
            }

            var windowsInstaller = ReadBoolean(applicationKey, "WindowsInstaller");
            var systemComponent = ReadBoolean(applicationKey, "SystemComponent");
            var source = windowsInstaller
                ? InstalledApplicationSourceKind.MsiProduct
                : InstalledApplicationSourceKind.RegistryUninstall;

            applications.Add(new InstalledApplicationSnapshot(
                CreateIdentity(location, keyName),
                location.Scope,
                location.Architecture,
                source,
                registryKeyPath,
                keyName,
                TryGetProductCode(keyName),
                displayName,
                Normalize(ReadString(applicationKey, "DisplayVersion")),
                Normalize(ReadString(applicationKey, "Publisher")),
                Normalize(ReadString(applicationKey, "InstallLocation")),
                Normalize(ReadString(applicationKey, "InstallSource")),
                Normalize(ReadString(applicationKey, "InstallDate")),
                Normalize(ReadString(applicationKey, "UninstallString")),
                Normalize(ReadString(applicationKey, "QuietUninstallString")),
                Normalize(ReadString(applicationKey, "ModifyPath")),
                ReadNullableInt64(applicationKey, "EstimatedSize"),
                windowsInstaller,
                systemComponent,
                Normalize(ReadString(applicationKey, "ReleaseType")),
                Normalize(ReadString(applicationKey, "Comments")),
                Normalize(ReadString(applicationKey, "URLInfoAbout"))));
        }
        catch (Exception ex) when (IsRegistryReadException(ex))
        {
            warnings.Add($"Installed application registry key could not be read: {registryKeyPath} - {ex.Message}");
        }
    }

    private static string CreateIdentity(RegistryUninstallLocation location, string keyName)
    {
        return $"{location.HiveName}|{location.View}|{UninstallSubKeyPath}\\{keyName}";
    }

    private static void ReadAppxLocation(
        AppxRegistryLocation location,
        List<InstalledApplicationSnapshot> applications,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
            using var packagesKey = baseKey.OpenSubKey(location.SubKeyPath);
            if (packagesKey is null)
            {
                return;
            }

            foreach (var keyName in packagesKey.GetSubKeyNames().Order(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadAppxPackageKey(location, packagesKey, keyName, applications, warnings);
            }
        }
        catch (Exception ex) when (IsRegistryReadException(ex))
        {
            warnings.Add($"AppX/MSIX package registry location could not be read: {location.DisplayName} - {ex.Message}");
        }
    }

    private static void ReadAppxPackageKey(
        AppxRegistryLocation location,
        RegistryKey packagesKey,
        string keyName,
        List<InstalledApplicationSnapshot> applications,
        List<string> warnings)
    {
        var registryKeyPath = $@"{location.HiveName}\{location.SubKeyPath}\{keyName}";

        try
        {
            using var packageKey = packagesKey.OpenSubKey(keyName);
            if (packageKey is null)
            {
                warnings.Add($"AppX/MSIX package registry key disappeared during collection: {registryKeyPath}");
                return;
            }

            var packageFullName = Normalize(ReadString(packageKey, "PackageID")) ?? keyName;
            var package = AppxPackageIdentityParser.Parse(packageFullName);
            var displayName = FriendlyAppxDisplayName(
                Normalize(ReadString(packageKey, "DisplayName")),
                package);
            var manifestPath = Normalize(ReadString(packageKey, "Path"));
            var packageRoot = Normalize(ReadString(packageKey, "PackageRootFolder")) ??
                TryGetPackageRootFromManifestPath(manifestPath);

            applications.Add(new InstalledApplicationSnapshot(
                CreateAppxIdentity(location, package),
                location.Scope,
                package.Architecture,
                InstalledApplicationSourceKind.AppxPackage,
                registryKeyPath,
                keyName,
                null,
                displayName,
                package.Version,
                null,
                packageRoot,
                location.RepositoryName,
                null,
                null,
                null,
                null,
                null,
                false,
                location.IsInboxPackage,
                null,
                null,
                null,
                package.FullName,
                package.FamilyName,
                package.Name,
                package.PublisherId,
                package.ResourceId,
                manifestPath,
                location.IsInboxPackage));
        }
        catch (Exception ex) when (IsRegistryReadException(ex))
        {
            warnings.Add($"AppX/MSIX package registry key could not be read: {registryKeyPath} - {ex.Message}");
        }
    }

    private static string CreateAppxIdentity(AppxRegistryLocation location, AppxPackageIdentity package)
    {
        return package.FamilyName is null
            ? $"APPX|{location.RepositoryName}|{package.FullName}"
            : $"APPX|{location.RepositoryName}|{package.Name}|{package.ArchitectureToken}|{package.ResourceId}|{package.PublisherId}";
    }

    private static string FriendlyAppxDisplayName(string? displayName, AppxPackageIdentity package)
    {
        if (string.IsNullOrWhiteSpace(displayName) ||
            displayName.StartsWith("@{", StringComparison.Ordinal))
        {
            return package.Name;
        }

        return displayName;
    }

    private static string? TryGetPackageRootFromManifestPath(string? manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return null;
        }

        var manifestDirectory = Path.GetDirectoryName(manifestPath);
        if (manifestDirectory is null)
        {
            return null;
        }

        if (string.Equals(Path.GetFileName(manifestDirectory), "AppxMetadata", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(manifestDirectory);
        }

        return manifestDirectory;
    }

    private static string? TryGetProductCode(string keyName)
    {
        var trimmed = keyName.Trim();
        return Guid.TryParse(trimmed.Trim('{', '}'), out _)
            ? trimmed
            : null;
    }

    private static bool ReadBoolean(RegistryKey key, string valueName)
    {
        var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value switch
        {
            int number => number != 0,
            long number => number != 0,
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number != 0,
            _ => false
        };
    }

    private static long? ReadNullableInt64(RegistryKey key, string valueName)
    {
        var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value switch
        {
            int number => number,
            long number => number,
            string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };
    }

    private static string? ReadString(RegistryKey key, string valueName)
    {
        var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value switch
        {
            string text => text,
            string[] values => string.Join(";", values),
            byte[] bytes => Convert.ToHexString(bytes),
            _ => value?.ToString()
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsRegistryReadException(Exception ex)
    {
        return ex is IOException or SecurityException or UnauthorizedAccessException or ArgumentException or InvalidOperationException;
    }

    private sealed record RegistryUninstallLocation(
        RegistryHive Hive,
        string HiveName,
        RegistryView View,
        InstalledApplicationScopeKind Scope,
        InstalledApplicationArchitectureKind Architecture)
    {
        public string DisplayName => $@"{HiveName}\{UninstallSubKeyPath} ({View})";
    }

    private sealed record AppxRegistryLocation(
        RegistryHive Hive,
        string HiveName,
        RegistryView View,
        string SubKeyPath,
        string RepositoryName,
        InstalledApplicationScopeKind Scope,
        bool IsInboxPackage)
    {
        public string DisplayName => $@"{HiveName}\{SubKeyPath} ({View})";
    }
}
