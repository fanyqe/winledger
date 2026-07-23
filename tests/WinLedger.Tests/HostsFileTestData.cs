using System.Security.Cryptography;
using System.Text;
using WinLedger.Domain.Hosts;

namespace WinLedger.Tests;

internal static class HostsFileTestData
{
    public const string DefaultPath = @"C:\Windows\System32\drivers\etc\hosts";

    public static HostsFileSnapshot Snapshot(
        Guid sessionId,
        string name,
        string content,
        string filePath = DefaultPath)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new HostsFileSnapshot(
            Guid.NewGuid(),
            sessionId,
            name,
            DateTimeOffset.UtcNow,
            filePath,
            true,
            content,
            Convert.ToBase64String(bytes),
            Convert.ToHexString(SHA256.HashData(bytes)),
            bytes.LongLength,
            DateTimeOffset.UtcNow,
            Lines(content),
            []);
    }

    public static HostsFileSnapshot Missing(
        Guid sessionId,
        string name,
        string filePath = DefaultPath)
    {
        return HostsFileSnapshot.Missing(sessionId, name, DateTimeOffset.UtcNow, filePath);
    }

    public static IReadOnlyList<HostsFileLineSnapshot> Lines(string content)
    {
        var result = new List<HostsFileLineSnapshot>();
        using var reader = new StringReader(content);

        var lineNumber = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            result.Add(new HostsFileLineSnapshot(lineNumber, line));
            lineNumber++;
        }

        return result;
    }
}
