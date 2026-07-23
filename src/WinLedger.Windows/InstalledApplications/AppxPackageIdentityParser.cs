using WinLedger.Domain.InstalledApplications;

namespace WinLedger.Windows.InstalledApplications;

public static class AppxPackageIdentityParser
{
    public static AppxPackageIdentity Parse(string packageFullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFullName);

        var parts = packageFullName.Split('_');
        if (parts.Length < 5)
        {
            return new AppxPackageIdentity(
                packageFullName,
                packageFullName,
                null,
                null,
                null,
                null,
                null,
                InstalledApplicationArchitectureKind.Unknown);
        }

        var publisherId = Normalize(parts[^1]);
        var resourceId = Normalize(parts[^2]);
        var architectureToken = Normalize(parts[^3]);
        var version = Normalize(parts[^4]);
        var packageName = string.Join("_", parts.Take(parts.Length - 4));
        var packageFamilyName = publisherId is null ? null : $"{packageName}_{publisherId}";

        return new AppxPackageIdentity(
            packageFullName,
            packageName,
            version,
            architectureToken,
            resourceId,
            publisherId,
            packageFamilyName,
            ParseArchitecture(architectureToken));
    }

    private static InstalledApplicationArchitectureKind ParseArchitecture(string? architectureToken)
    {
        return architectureToken?.ToLowerInvariant() switch
        {
            "x86" => InstalledApplicationArchitectureKind.X86,
            "x64" => InstalledApplicationArchitectureKind.X64,
            "arm64" => InstalledApplicationArchitectureKind.Arm64,
            "neutral" => InstalledApplicationArchitectureKind.Neutral,
            _ => InstalledApplicationArchitectureKind.Unknown
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record AppxPackageIdentity(
    string FullName,
    string Name,
    string? Version,
    string? ArchitectureToken,
    string? ResourceId,
    string? PublisherId,
    string? FamilyName,
    InstalledApplicationArchitectureKind Architecture);
