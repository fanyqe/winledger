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

    [Fact]
    public async Task FileVerifierComputesAndMatchesFileHashes()
    {
        var path = Path.Combine(Path.GetTempPath(), "WinLedgerTests", $"{Guid.NewGuid():N}.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            await File.WriteAllTextAsync(path, "hash me");

            var hash = await ElevatedHelperFileVerifier.ComputeSha256Async(path, CancellationToken.None);

            Assert.True(ElevatedHelperFileVerifier.MatchesSha256(hash, hash.ToLowerInvariant()));
            Assert.False(ElevatedHelperFileVerifier.MatchesSha256(hash, new string('0', hash.Length)));
            Assert.False(ElevatedHelperFileVerifier.MatchesSha256(hash, "not-a-hash"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
