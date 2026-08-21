namespace NetCheck.Core.Models;

public sealed record DiagnosticOptions
{
    public string DnsTestHost { get; init; } = "www.microsoft.com";

    public IReadOnlyList<string> InternetPingTargets { get; init; } = ["1.1.1.1", "8.8.8.8"];

    public Uri ConnectivityCheckUri { get; init; } =
        new("http://www.msftconnecttest.com/connecttest.txt");

    public string ConnectivityExpectedContent { get; init; } = "Microsoft Connect Test";

    public int PingTimeoutMilliseconds { get; init; } = 1500;

    public int HttpTimeoutSeconds { get; init; } = 8;

    public int StabilitySampleCount { get; init; } = 6;

    public double PacketLossWarningPercent { get; init; } = 15;

    public double LatencyWarningMilliseconds { get; init; } = 180;

    public bool AutoRunOnLaunch { get; init; } = true;

    public bool SaveDiagnosticHistory { get; init; } = true;

    public bool IncludeComputerNameInExports { get; init; }

    public string MenuLanguage { get; init; } = "en";
}
