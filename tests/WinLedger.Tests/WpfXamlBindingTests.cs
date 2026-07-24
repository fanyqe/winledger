namespace WinLedger.Tests;

public sealed class WpfXamlBindingTests
{
    [Fact]
    public async Task MainWindowProgressBindingsDoNotWriteToReadOnlyViewModelProperties()
    {
        var xamlPath = FindRepositoryFile("src", "WinLedger.App", "MainWindow.xaml");
        var xaml = await File.ReadAllTextAsync(xamlPath);

        Assert.Contains(
            "Value=\"{Binding UnifiedCaptureProgressPercent, Mode=OneWay}\"",
            xaml,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Repository file was not found.", Path.Combine(relativeParts));
    }
}
