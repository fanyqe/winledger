using System.Security.Cryptography;
using System.Text;

namespace WinLedger.Storage.Sqlite;

internal static class SqliteSnapshotPayloadProtector
{
    private const string ProtectedPayloadPrefix = "winledger-dpapi:v1:";

    private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes(
        "WinLedger.SqliteSnapshotPayload.v1");

    public static bool IsProtected(string payload)
    {
        return payload.StartsWith(ProtectedPayloadPrefix, StringComparison.Ordinal);
    }

    public static string Protect(string plaintextJson)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Snapshot payload protection requires Windows DPAPI.");
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextJson);
        try
        {
            var protectedBytes = ProtectedData.Protect(
                plaintextBytes,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);
            return ProtectedPayloadPrefix + Convert.ToBase64String(protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    public static string UnprotectIfNeeded(string storedPayload)
    {
        if (!IsProtected(storedPayload))
        {
            return storedPayload;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Snapshot payload protection requires Windows DPAPI.");
        }

        var protectedBytes = Convert.FromBase64String(storedPayload[ProtectedPayloadPrefix.Length..]);
        var plaintextBytes = ProtectedData.Unprotect(
            protectedBytes,
            AdditionalEntropy,
            DataProtectionScope.CurrentUser);
        try
        {
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }
}
