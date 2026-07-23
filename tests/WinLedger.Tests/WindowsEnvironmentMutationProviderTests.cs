using WinLedger.Domain.EnvironmentVariables;
using WinLedger.Windows.EnvironmentVariables;

namespace WinLedger.Tests;

public sealed class WindowsEnvironmentMutationProviderTests
{
    [Fact]
    public async Task ReadVariableAsyncReturnsNullForMissingUserVariable()
    {
        var provider = new WindowsEnvironmentMutationProvider();

        var variable = await provider.ReadVariableAsync(
            EnvironmentVariableScopeKind.User,
            $"WINLEDGER_MISSING_{Guid.NewGuid():N}",
            CancellationToken.None);

        Assert.Null(variable);
    }
}
