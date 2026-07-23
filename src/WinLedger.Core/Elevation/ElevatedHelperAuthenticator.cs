using System.Security.Cryptography;
using System.Text;

namespace WinLedger.Core.Elevation;

public static class ElevatedHelperAuthenticator
{
    public static string GenerateToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    public static string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Authentication token is required.", nameof(token));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public static bool Matches(string token, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(expectedSha256))
        {
            return false;
        }

        byte[] actualBytes;
        byte[] expectedBytes;
        try
        {
            actualBytes = Convert.FromHexString(HashToken(token));
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
