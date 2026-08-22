namespace NetCheck.Core.Models;

public enum MonitoringProfile
{
    Standard,
    Gaming,
    Streaming,
    HomeOffice
}

public enum MonitoringRunLength
{
    FifteenMinutes,
    ThirtyMinutes,
    SixtyMinutes,
    Continuous
}

public enum ConnectionState
{
    Unknown,
    Online,
    Degraded,
    Offline
}

public enum MonitoringEventKind
{
    SessionStarted,
    SessionCompleted,
    SessionStopped,
    OutageStarted,
    ConnectionRecovered,
    QualityDegraded,
    QualityRecovered,
    AdapterChanged,
    GatewayChanged,
    ProbeIssue
}

public enum BaselineTrend
{
    NoBaseline,
    Better,
    Similar,
    Worse
}

public sealed record MonitoringOptions
{
    public MonitoringProfile Profile { get; init; } = MonitoringProfile.Standard;

    public MonitoringRunLength RunLength { get; init; } = MonitoringRunLength.FifteenMinutes;

    public TimeSpan? Duration { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan SamplingInterval { get; init; } = TimeSpan.FromSeconds(2);

    public int PingTimeoutMilliseconds { get; init; } = 1200;

    public double LatencyWarningMilliseconds { get; init; } = 150;

    public double JitterWarningMilliseconds { get; init; } = 35;

    public double PacketLossWarningPercent { get; init; } = 5;

    public int RollingWindowSize { get; init; } = 20;

    public string Ipv4Target { get; init; } = "1.1.1.1";

    public string Ipv6Target { get; init; } = "2606:4700:4700::1111";

    public string DnsTestHost { get; init; } = "www.microsoft.com";

    public Uri WebFallbackUri { get; init; } =
        new("https://www.msftconnecttest.com/connecttest.txt");

    public int MaximumTracerouteHops { get; init; } = 12;

    public static MonitoringOptions Create(
        MonitoringProfile profile,
        MonitoringRunLength runLength)
    {
        var duration = runLength switch
        {
            MonitoringRunLength.FifteenMinutes => TimeSpan.FromMinutes(15),
            MonitoringRunLength.ThirtyMinutes => TimeSpan.FromMinutes(30),
            MonitoringRunLength.SixtyMinutes => TimeSpan.FromMinutes(60),
            _ => (TimeSpan?)null
        };

        var profileSettings = profile switch
        {
            MonitoringProfile.Gaming => new
            {
                Interval = TimeSpan.FromSeconds(1),
                Timeout = 900,
                Latency = 70d,
                Jitter = 18d,
                Loss = 1d
            },
            MonitoringProfile.Streaming => new
            {
                Interval = TimeSpan.FromSeconds(2),
                Timeout = 1200,
                Latency = 120d,
                Jitter = 35d,
                Loss = 3d
            },
            MonitoringProfile.HomeOffice => new
            {
                Interval = TimeSpan.FromSeconds(2),
                Timeout = 1200,
                Latency = 100d,
                Jitter = 25d,
                Loss = 2d
            },
            _ => new
            {
                Interval = TimeSpan.FromSeconds(2),
                Timeout = 1200,
                Latency = 150d,
                Jitter = 35d,
                Loss = 5d
            }
        };

        return new MonitoringOptions
        {
            Profile = profile,
            RunLength = runLength,
            Duration = duration,
            SamplingInterval = profileSettings.Interval,
            PingTimeoutMilliseconds = profileSettings.Timeout,
            LatencyWarningMilliseconds = profileSettings.Latency,
            JitterWarningMilliseconds = profileSettings.Jitter,
            PacketLossWarningPercent = profileSettings.Loss
        };
    }
}

public sealed record NetworkMonitoringProbeResult
{
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public double? Ipv4LatencyMilliseconds { get; init; }

    public double? Ipv6LatencyMilliseconds { get; init; }

    public bool DnsIpv4Resolved { get; init; }

    public bool DnsIpv6Resolved { get; init; }

    public bool WebReachable { get; init; }

    public string AdapterId { get; init; } = string.Empty;

    public string AdapterName { get; init; } = string.Empty;

    public string Gateway { get; init; } = string.Empty;

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed record MonitoringSample
{
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public double? Ipv4LatencyMilliseconds { get; init; }

    public double? Ipv6LatencyMilliseconds { get; init; }

    public double? PreferredLatencyMilliseconds { get; init; }

    public double JitterMilliseconds { get; init; }

    public double RollingPacketLossPercent { get; init; }

    public bool DnsIpv4Resolved { get; init; }

    public bool DnsIpv6Resolved { get; init; }

    public bool WebReachable { get; init; }

    public ConnectionState State { get; init; }

    public string AdapterId { get; init; } = string.Empty;

    public string AdapterName { get; init; } = string.Empty;

    public string Gateway { get; init; } = string.Empty;

    public IReadOnlyList<string> ProbeErrors { get; init; } = Array.Empty<string>();
}

public sealed record MonitoringEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public MonitoringEventKind Kind { get; init; }

    public ConnectionState PreviousState { get; init; }

    public ConnectionState NewState { get; init; }

    public TimeSpan? Duration { get; init; }

    public string Detail { get; init; } = string.Empty;
}

public sealed record RouteHop
{
    public int Hop { get; init; }

    public string Address { get; init; } = "*";

    public double? LatencyMilliseconds { get; init; }

    public bool ReachedDestination { get; init; }
}

public sealed record WifiNetworkDetails
{
    public bool IsConnected { get; init; }

    public string Ssid { get; init; } = string.Empty;

    public int? SignalQualityPercent { get; init; }

    public int? Channel { get; init; }

    public string Band { get; init; } = string.Empty;

    public string RadioType { get; init; } = string.Empty;

    public double? ReceiveRateMegabitsPerSecond { get; init; }

    public double? TransmitRateMegabitsPerSecond { get; init; }
}

public sealed record FirewallProfileStatus
{
    public bool DomainEnabled { get; init; }

    public bool PrivateEnabled { get; init; }

    public bool PublicEnabled { get; init; }
}

public sealed record WindowsNetworkEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; }

    public string Provider { get; init; } = string.Empty;

    public int EventId { get; init; }

    public string Level { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public Guid? RelatedMonitoringEventId { get; init; }
}

public sealed record NetworkEnvironmentSnapshot
{
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string AdapterId { get; init; } = string.Empty;

    public string AdapterName { get; init; } = string.Empty;

    public string AdapterDescription { get; init; } = string.Empty;

    public string InterfaceType { get; init; } = string.Empty;

    public string DriverVersion { get; init; } = string.Empty;

    public long LinkSpeedBitsPerSecond { get; init; }

    public bool SupportsIpv4 { get; init; }

    public bool SupportsIpv6 { get; init; }

    public WifiNetworkDetails? Wifi { get; init; }

    public IReadOnlyList<string> VpnAdapters { get; init; } = Array.Empty<string>();

    public FirewallProfileStatus Firewall { get; init; } = new();

    public IReadOnlyList<RouteHop> Ipv4Route { get; init; } = Array.Empty<RouteHop>();

    public IReadOnlyList<RouteHop> Ipv6Route { get; init; } = Array.Empty<RouteHop>();

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed record MonitoringSummary
{
    public int TotalSamples { get; init; }

    public int SuccessfulSamples { get; init; }

    public int OfflineSamples { get; init; }

    public int OutageCount { get; init; }

    public double AvailabilityPercent { get; init; }

    public double PacketLossPercent { get; init; }

    public double AverageLatencyMilliseconds { get; init; }

    public double MaximumLatencyMilliseconds { get; init; }

    public double AverageJitterMilliseconds { get; init; }

    public TimeSpan TotalOutageDuration { get; init; }

    public bool Ipv4Available { get; init; }

    public bool Ipv6Available { get; init; }
}

public sealed record BaselineComparison
{
    public BaselineTrend Trend { get; init; } = BaselineTrend.NoBaseline;

    public int ComparedSessionCount { get; init; }

    public double LatencyDifferencePercent { get; init; }

    public double PacketLossDifferencePercentagePoints { get; init; }

    public double AvailabilityDifferencePercentagePoints { get; init; }
}

public sealed record MonitoringSession
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CompletedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public MonitoringProfile Profile { get; init; }

    public MonitoringRunLength RunLength { get; init; }

    public bool WasStopped { get; init; }

    public NetworkEnvironmentSnapshot Environment { get; init; } = new();

    public NetworkEnvironmentSnapshot? FinalEnvironment { get; init; }

    public IReadOnlyList<MonitoringSample> Samples { get; init; } =
        Array.Empty<MonitoringSample>();

    public IReadOnlyList<MonitoringEvent> Events { get; init; } =
        Array.Empty<MonitoringEvent>();

    public IReadOnlyList<WindowsNetworkEvent> WindowsEvents { get; init; } =
        Array.Empty<WindowsNetworkEvent>();

    public MonitoringSummary Summary { get; init; } = new();

    public BaselineComparison Baseline { get; init; } = new();

    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;
}

public sealed record MonitoringProgress
{
    public required MonitoringSample LatestSample { get; init; }

    public IReadOnlyList<MonitoringEvent> NewEvents { get; init; } =
        Array.Empty<MonitoringEvent>();

    public required MonitoringSummary Summary { get; init; }

    public required NetworkEnvironmentSnapshot Environment { get; init; }

    public TimeSpan Elapsed { get; init; }

    public TimeSpan? TargetDuration { get; init; }
}
