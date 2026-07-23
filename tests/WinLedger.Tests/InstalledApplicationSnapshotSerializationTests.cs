using System.Text.Json;
using WinLedger.Domain;
using WinLedger.Domain.InstalledApplications;

namespace WinLedger.Tests;

public sealed class InstalledApplicationSnapshotSerializationTests
{
    [Fact]
    public void DeserializeAcceptsRegistryOnlySnapshotsWithoutPackageFields()
    {
        const string json = """
        {
          "identity": "HKLM|Registry64|SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ExampleApp",
          "scope": "Machine",
          "architecture": "X64",
          "source": "RegistryUninstall",
          "registryKeyPath": "HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ExampleApp",
          "keyName": "ExampleApp",
          "productCode": null,
          "displayName": "Example App",
          "displayVersion": "1.0.0",
          "publisher": "Example Publisher",
          "installLocation": "C:\\Program Files\\Example",
          "installSource": "C:\\Installers",
          "installDate": "20260723",
          "uninstallString": "\"C:\\Program Files\\Example\\uninstall.exe\"",
          "quietUninstallString": "\"C:\\Program Files\\Example\\uninstall.exe\" /quiet",
          "modifyPath": "\"C:\\Program Files\\Example\\setup.exe\" /modify",
          "estimatedSizeKb": 1024,
          "windowsInstaller": false,
          "systemComponent": false,
          "releaseType": null,
          "comments": "Example comments",
          "urlInfoAbout": "https://example.test"
        }
        """;

        var snapshot = JsonSerializer.Deserialize<InstalledApplicationSnapshot>(json, WinLedgerJsonSerializer.Options);

        Assert.NotNull(snapshot);
        Assert.Equal("Example App", snapshot.DisplayName);
        Assert.Null(snapshot.PackageFullName);
        Assert.False(snapshot.AppxInboxPackage);
    }
}
