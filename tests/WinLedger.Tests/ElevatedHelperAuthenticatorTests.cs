using WinLedger.Core.Elevation;

namespace WinLedger.Tests;

public sealed class ElevatedHelperAuthenticatorTests
{
    [Fact]
    public void MatchesAcceptsTheOriginalToken()
    {
        var token = ElevatedHelperAuthenticator.GenerateToken();
        var hash = ElevatedHelperAuthenticator.HashToken(token);

        Assert.True(ElevatedHelperAuthenticator.Matches(token, hash));
    }

    [Fact]
    public void MatchesRejectsDifferentTokens()
    {
        var hash = ElevatedHelperAuthenticator.HashToken(ElevatedHelperAuthenticator.GenerateToken());

        Assert.False(ElevatedHelperAuthenticator.Matches(ElevatedHelperAuthenticator.GenerateToken(), hash));
    }

    [Fact]
    public void MatchesRejectsMalformedHashes()
    {
        var token = ElevatedHelperAuthenticator.GenerateToken();

        Assert.False(ElevatedHelperAuthenticator.Matches(token, "not-a-hash"));
    }
}
