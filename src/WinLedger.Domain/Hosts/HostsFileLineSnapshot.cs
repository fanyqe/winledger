namespace WinLedger.Domain.Hosts;

public sealed record HostsFileLineSnapshot(
    int LineNumber,
    string Text);
