using WinLedger.Domain.InstalledApplications;
using WinLedger.Windows.InstalledApplications;

namespace WinLedger.Tests;

public sealed class AppxPackageIdentityParserTests
{
    [Fact]
    public void ParseExtractsPackageIdentityParts()
    {
        var identity = AppxPackageIdentityParser.Parse("Microsoft.WindowsCalculator_11.2405.2.0_x64__8wekyb3d8bbwe");

        Assert.Equal("Microsoft.WindowsCalculator", identity.Name);
        Assert.Equal("11.2405.2.0", identity.Version);
        Assert.Equal("x64", identity.ArchitectureToken);
        Assert.Null(identity.ResourceId);
        Assert.Equal("8wekyb3d8bbwe", identity.PublisherId);
        Assert.Equal("Microsoft.WindowsCalculator_8wekyb3d8bbwe", identity.FamilyName);
        Assert.Equal(InstalledApplicationArchitectureKind.X64, identity.Architecture);
    }

    [Fact]
    public void ParseHandlesSplitResourceAndArm64Architecture()
    {
        var identity = AppxPackageIdentityParser.Parse("Vendor.App_2.0.0.0_arm64_split.scale-200_publisherid");

        Assert.Equal("Vendor.App", identity.Name);
        Assert.Equal("2.0.0.0", identity.Version);
        Assert.Equal("arm64", identity.ArchitectureToken);
        Assert.Equal("split.scale-200", identity.ResourceId);
        Assert.Equal(InstalledApplicationArchitectureKind.Arm64, identity.Architecture);
    }
}
