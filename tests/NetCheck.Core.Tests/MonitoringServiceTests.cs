using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;
using NetCheck.Core.Monitoring;

namespace NetCheck.Core.Tests;

public sealed class MonitoringServiceTests
{
    [Fact]
    public async Task RunAsync_TracksOutageRecoverySummaryAndEventCorrelation()
    {
        using var cancellation = new CancellationTokenSource();
        var started = DateTimeOffset.UtcNow;
        var probe = new SequenceProbe(
        [
            Result(started, 20),
            Result(started.AddSeconds(1), null),
            Result(started.AddSeconds(3), 30)
        ], cancellation, new WindowsNetworkEvent
        {
            OccurredAtUtc = started.AddSeconds(1),
            Provider = "Microsoft-Windows-NetworkProfile",
            EventId = 10001
        });
        var progressEvents = new List<MonitoringProgress>();
        var service = new MonitoringService(probe);

        var session = await service.RunAsync(
            ContinuousOptions(),
            new SynchronousProgress<MonitoringProgress>(progressEvents.Add),
            cancellation.Token);

        Assert.True(session.WasStopped);
        Assert.Equal(3, session.Samples.Count);
        Assert.Equal(1, session.Summary.OutageCount);
        Assert.Equal(2, session.Summary.SuccessfulSamples);
        Assert.Equal(3, progressEvents.Count);
        var outage = Assert.Single(session.Events, item => item.Kind == MonitoringEventKind.OutageStarted);
        var recovery = Assert.Single(session.Events, item => item.Kind == MonitoringEventKind.ConnectionRecovered);
        Assert.Equal(TimeSpan.FromSeconds(2), recovery.Duration);
        Assert.Equal(outage.Id, Assert.Single(session.WindowsEvents).RelatedMonitoringEventId);
    }

    [Fact]
    public async Task RunAsync_WhenAProbeThrows_ContinuesAndRecordsIssue()
    {
        using var cancellation = new CancellationTokenSource();
        var probe = new ThrowingThenSuccessfulProbe(cancellation);
        var service = new MonitoringService(probe);

        var session = await service.RunAsync(
            ContinuousOptions(),
            progress: null,
            cancellation.Token);

        Assert.Equal(2, session.Samples.Count);
        Assert.Equal(ConnectionState.Offline, session.Samples[0].State);
        Assert.NotEqual(ConnectionState.Offline, session.Samples[1].State);
        Assert.Contains(session.Events, item => item.Kind == MonitoringEventKind.ProbeIssue);
        Assert.Contains(session.Events, item => item.Kind == MonitoringEventKind.ConnectionRecovered);
    }

    private static MonitoringOptions ContinuousOptions() => MonitoringOptions.Create(
        MonitoringProfile.Standard,
        MonitoringRunLength.Continuous) with
    {
        SamplingInterval = TimeSpan.FromMilliseconds(1),
        RollingWindowSize = 3
    };

    private static NetworkMonitoringProbeResult Result(DateTimeOffset capturedAtUtc, double? latency) => new()
    {
        CapturedAtUtc = capturedAtUtc,
        Ipv4LatencyMilliseconds = latency,
        DnsIpv4Resolved = latency is not null,
        WebReachable = latency is not null,
        AdapterId = "adapter",
        AdapterName = "Ethernet",
        Gateway = "gateway"
    };

    private sealed class SequenceProbe(
        IReadOnlyList<NetworkMonitoringProbeResult> results,
        CancellationTokenSource cancellation,
        WindowsNetworkEvent windowsEvent) : INetworkMonitoringProbe
    {
        private int _index;

        public Task<NetworkEnvironmentSnapshot> CaptureEnvironmentAsync(
            MonitoringOptions options,
            CancellationToken cancellationToken) => Task.FromResult(new NetworkEnvironmentSnapshot
            {
                AdapterId = "adapter",
                AdapterName = "Ethernet",
                SupportsIpv4 = true
            });

        public Task<NetworkMonitoringProbeResult> ProbeAsync(
            MonitoringOptions options,
            CancellationToken cancellationToken)
        {
            var result = results[_index++];
            if (_index == results.Count)
            {
                cancellation.Cancel();
            }

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<WindowsNetworkEvent>> GetWindowsNetworkEventsAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WindowsNetworkEvent>>([windowsEvent]);
    }

    private sealed class ThrowingThenSuccessfulProbe(CancellationTokenSource cancellation)
        : INetworkMonitoringProbe
    {
        private int _count;

        public Task<NetworkEnvironmentSnapshot> CaptureEnvironmentAsync(
            MonitoringOptions options,
            CancellationToken cancellationToken) => Task.FromResult(new NetworkEnvironmentSnapshot());

        public Task<NetworkMonitoringProbeResult> ProbeAsync(
            MonitoringOptions options,
            CancellationToken cancellationToken)
        {
            _count++;
            if (_count == 1)
            {
                throw new InvalidOperationException("Simulated probe failure");
            }

            cancellation.Cancel();
            return Task.FromResult(Result(DateTimeOffset.UtcNow, 12));
        }

        public Task<IReadOnlyList<WindowsNetworkEvent>> GetWindowsNetworkEventsAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WindowsNetworkEvent>>(Array.Empty<WindowsNetworkEvent>());
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
