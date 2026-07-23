using System.Security.Cryptography;

namespace WinLedger.Core.Elevation;

public static class ElevatedHelperFileVerifier
{
    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    public static bool MatchesSha256(string? actualSha256, string? expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(actualSha256) || string.IsNullOrWhiteSpace(expectedSha256))
        {
            return false;
        }

        byte[] actualBytes;
        byte[] expectedBytes;
        try
        {
            actualBytes = Convert.FromHexString(actualSha256);
            expectedBytes = Convert.FromHexString(expectedSha256);
        }
        catch (FormatException)
        {
            return false;
        }

        return actualBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
