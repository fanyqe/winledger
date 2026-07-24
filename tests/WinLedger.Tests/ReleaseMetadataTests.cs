using System.Xml.Linq;

namespace WinLedger.Tests;

public sealed class ReleaseMetadataTests
{
    [Fact]
    public void ReleaseDocumentationDoesNotUseAlphaLanguage()
    {
        var repoRoot = FindRepositoryRoot();
        var files = Directory.EnumerateFiles(repoRoot, "*.md", SearchOption.TopDirectoryOnly).ToList();
        files.AddRange(Directory.EnumerateFiles(Path.Combine(repoRoot, "docs"), "*.md", SearchOption.AllDirectories));

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            Assert.DoesNotContain("alpha", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pre-release", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("prerelease", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ExecutableProjectsDeclareReleaseFileMetadata()
    {
        var repoRoot = FindRepositoryRoot();
        var directoryProps = XDocument.Load(Path.Combine(repoRoot, "Directory.Build.props"));

        AssertProperty(directoryProps, "Version", "0.1.0");
        AssertProperty(directoryProps, "FileVersion", "0.1.0.0");
        AssertProperty(directoryProps, "InformationalVersion", "0.1.0");
        AssertProperty(directoryProps, "IncludeSourceRevisionInInformationalVersion", "false");
        AssertProperty(directoryProps, "Product", "WinLedger");
        AssertProperty(directoryProps, "Company", "WinLedger Contributors");

        AssertProjectMetadata(
            repoRoot,
            "src/WinLedger.App/WinLedger.App.csproj",
            "WinLedger",
            "Windows system change tracking and rollback reporting desktop app.");
        AssertProjectMetadata(
            repoRoot,
            "src/WinLedger.Cli/WinLedger.Cli.csproj",
            "WinLedger CLI",
            "Command line tools for WinLedger tracking sessions and rollback reports.");
        AssertProjectMetadata(
            repoRoot,
            "src/WinLedger.ElevatedHelper/WinLedger.ElevatedHelper.csproj",
            "WinLedger Elevated Helper",
            "Elevated rollback helper for validated WinLedger restore operations.");
    }

    private static void AssertProjectMetadata(string repoRoot, string relativePath, string expectedTitle, string expectedDescription)
    {
        var project = XDocument.Load(Path.Combine(repoRoot, relativePath));

        AssertProperty(project, "AssemblyTitle", expectedTitle);
        AssertProperty(project, "Description", expectedDescription);
    }

    private static void AssertProperty(XDocument document, string propertyName, string expectedValue)
    {
        var actual = document.Descendants(propertyName).SingleOrDefault()?.Value;

        Assert.Equal(expectedValue, actual);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WinLedger.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
