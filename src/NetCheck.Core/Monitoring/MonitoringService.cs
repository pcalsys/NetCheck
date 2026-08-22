using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Core.Monitoring;

public sealed class MonitoringService : IMonitoringService
{
    private readonly INetworkMonitoringProbe _probe;
    private readonly TimeProvider _timeProvider;

    public MonitoringService(
        INetworkMonitoringProbe probe,
        TimeProvider? timeProvider = null)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MonitoringSession> RunAsync(
        MonitoringOptions options,
        IProgress<MonitoringProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        var startedAtUtc = _timeProvider.GetUtcNow();
        var samples = new List<MonitoringSample>();
        var events = new List<MonitoringEvent>
        {
            new()
            {
                OccurredAtUtc = startedAtUtc,
                Kind = MonitoringEventKind.SessionStarted,
                NewState = ConnectionState.Unknown
            }
        };
        var environment = await CaptureEnvironmentSafelyAsync(options).ConfigureAwait(false);
        var currentEnvironment = environment;
        var previousState = ConnectionState.Unknown;
        string? previousAdapter = null;
        string? previousGateway = null;
        DateTimeOffset? outageStartedAtUtc = null;
        var previousErrorSignature = string.Empty;
        var stopped = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var elapsed = _timeProvider.GetUtcNow() - startedAtUtc;
                if (options.Duration is not null && elapsed >= options.Duration.Value && samples.Count > 0)
                {
                    break;
                }

                var result = await ProbeSafelyAsync(options, cancellationToken).ConfigureAwait(false);
                var sample = CreateSample(result, samples, options);
                samples.Add(sample);
                var newEvents = CreateTransitionEvents(
                    sample,
                    previousState,
                    previousAdapter,
                    previousGateway,
                    ref outageStartedAtUtc);
                var errorSignature = string.Join(" | ", sample.ProbeErrors);
                if (errorSignature.Length > 0
                    && !string.Equals(errorSignature, previousErrorSignature, StringComparison.Ordinal))
                {
                    newEvents = newEvents.Append(new MonitoringEvent
                    {
                        OccurredAtUtc = sample.CapturedAtUtc,
                        Kind = MonitoringEventKind.ProbeIssue,
                        PreviousState = previousState,
                        NewState = sample.State,
                        Detail = errorSignature
                    }).ToArray();
                }

                previousErrorSignature = errorSignature;
                events.AddRange(newEvents);
                previousState = sample.State;
                previousAdapter = sample.AdapterId;
                previousGateway = sample.Gateway;

                var summary = CalculateSummary(samples, events, sample.CapturedAtUtc, outageStartedAtUtc);
                progress?.Report(new MonitoringProgress
                {
                    LatestSample = sample,
                    NewEvents = newEvents,
                    Summary = summary,
                    Environment = currentEnvironment,
                    Elapsed = sample.CapturedAtUtc - startedAtUtc,
                    TargetDuration = options.Duration
                });

                if (options.Duration is not null
                    && sample.CapturedAtUtc - startedAtUtc >= options.Duration.Value)
                {
                    break;
                }

                await Task.Delay(options.SamplingInterval, _timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopped = true;
        }

        stopped |= cancellationToken.IsCancellationRequested;
        var completedAtUtc = _timeProvider.GetUtcNow();
        if (outageStartedAtUtc is not null)
        {
            var openOutage = events.FindLastIndex(item =>
                item.Kind == MonitoringEventKind.OutageStarted && item.Duration is null);
            if (openOutage >= 0)
            {
                events[openOutage] = events[openOutage] with
                {
                    Duration = completedAtUtc - outageStartedAtUtc.Value
                };
            }
        }

        events.Add(new MonitoringEvent
        {
            OccurredAtUtc = completedAtUtc,
            Kind = stopped ? MonitoringEventKind.SessionStopped : MonitoringEventKind.SessionCompleted,
            PreviousState = previousState,
            NewState = previousState
        });

        currentEnvironment = await CaptureEnvironmentSafelyAsync(options).ConfigureAwait(false);
        var windowsEvents = await GetWindowsEventsSafelyAsync(startedAtUtc, completedAtUtc)
            .ConfigureAwait(false);
        var correlatedEvents = CorrelateWindowsEvents(windowsEvents, events);

        return new MonitoringSession
        {
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Profile = options.Profile,
            RunLength = options.RunLength,
            WasStopped = stopped,
            Environment = environment,
            FinalEnvironment = currentEnvironment,
            Samples = samples,
            Events = events,
            WindowsEvents = correlatedEvents,
            Summary = CalculateSummary(samples, events, completedAtUtc, null)
        };
    }

    private static void Validate(MonitoringOptions options)
    {
        if (options.SamplingInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The sampling interval must be positive.");
        }

        if (options.Duration is { } duration && duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The duration must be positive.");
        }

        if (options.RollingWindowSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The rolling window size must be positive.");
        }
    }

    private async Task<NetworkEnvironmentSnapshot> CaptureEnvironmentSafelyAsync(
        MonitoringOptions options)
    {
        try
        {
            return await _probe.CaptureEnvironmentAsync(options, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return new NetworkEnvironmentSnapshot
            {
                CapturedAtUtc = _timeProvider.GetUtcNow(),
                Errors = [$"Environment: {exception.GetType().Name}: {exception.Message}"]
            };
        }
    }

    private async Task<NetworkMonitoringProbeResult> ProbeSafelyAsync(
        MonitoringOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _probe.ProbeAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new NetworkMonitoringProbeResult
            {
                CapturedAtUtc = _timeProvider.GetUtcNow(),
                Errors = [$"Probe: {exception.GetType().Name}: {exception.Message}"]
            };
        }
    }

    private async Task<IReadOnlyList<WindowsNetworkEvent>> GetWindowsEventsSafelyAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        try
        {
            return await _probe.GetWindowsNetworkEventsAsync(fromUtc, toUtc, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<WindowsNetworkEvent>();
        }
    }

    private static MonitoringSample CreateSample(
        NetworkMonitoringProbeResult result,
        IReadOnlyList<MonitoringSample> previousSamples,
        MonitoringOptions options)
    {
        var preferredLatency = result.Ipv4LatencyMilliseconds ?? result.Ipv6LatencyMilliseconds;
        var recent = previousSamples
            .TakeLast(Math.Max(0, options.RollingWindowSize - 1))
            .Select(sample => sample.PreferredLatencyMilliseconds)
            .Append(preferredLatency)
            .ToArray();
        var rollingLoss = recent.Count(value => value is null) * 100d / recent.Length;
        var successfulLatencies = recent.OfType<double>().ToArray();
        var jitter = successfulLatencies.Length < 2
            ? 0
            : successfulLatencies.Zip(successfulLatencies.Skip(1), (left, right) => Math.Abs(right - left)).Average();
        var reachable = preferredLatency is not null || result.WebReachable;
        var hasNameResolution = result.DnsIpv4Resolved || result.DnsIpv6Resolved;
        var state = !reachable
            ? ConnectionState.Offline
            : preferredLatency is null
              || !result.WebReachable
              || !hasNameResolution
              || preferredLatency > options.LatencyWarningMilliseconds
              || jitter > options.JitterWarningMilliseconds
              || rollingLoss > options.PacketLossWarningPercent
                ? ConnectionState.Degraded
                : ConnectionState.Online;

        return new MonitoringSample
        {
            CapturedAtUtc = result.CapturedAtUtc,
            Ipv4LatencyMilliseconds = result.Ipv4LatencyMilliseconds,
            Ipv6LatencyMilliseconds = result.Ipv6LatencyMilliseconds,
            PreferredLatencyMilliseconds = preferredLatency,
            JitterMilliseconds = jitter,
            RollingPacketLossPercent = rollingLoss,
            DnsIpv4Resolved = result.DnsIpv4Resolved,
            DnsIpv6Resolved = result.DnsIpv6Resolved,
            WebReachable = result.WebReachable,
            State = state,
            AdapterId = result.AdapterId,
            AdapterName = result.AdapterName,
            Gateway = result.Gateway,
            ProbeErrors = result.Errors
        };
    }

    private static IReadOnlyList<MonitoringEvent> CreateTransitionEvents(
        MonitoringSample sample,
        ConnectionState previousState,
        string? previousAdapter,
        string? previousGateway,
        ref DateTimeOffset? outageStartedAtUtc)
    {
        var events = new List<MonitoringEvent>();
        if (previousState != sample.State
            && (previousState != ConnectionState.Unknown || sample.State == ConnectionState.Offline))
        {
            var kind = (previousState, sample.State) switch
            {
                (_, ConnectionState.Offline) => MonitoringEventKind.OutageStarted,
                (ConnectionState.Offline, _) => MonitoringEventKind.ConnectionRecovered,
                (ConnectionState.Online, ConnectionState.Degraded) => MonitoringEventKind.QualityDegraded,
                (ConnectionState.Degraded, ConnectionState.Online) => MonitoringEventKind.QualityRecovered,
                _ => MonitoringEventKind.QualityDegraded
            };
            TimeSpan? duration = null;
            if (kind == MonitoringEventKind.OutageStarted)
            {
                outageStartedAtUtc = sample.CapturedAtUtc;
            }
            else if (kind == MonitoringEventKind.ConnectionRecovered && outageStartedAtUtc is not null)
            {
                duration = sample.CapturedAtUtc - outageStartedAtUtc.Value;
                outageStartedAtUtc = null;
            }

            events.Add(new MonitoringEvent
            {
                OccurredAtUtc = sample.CapturedAtUtc,
                Kind = kind,
                PreviousState = previousState,
                NewState = sample.State,
                Duration = duration
            });
        }

        if (previousAdapter is not null
            && !string.Equals(previousAdapter, sample.AdapterId, StringComparison.OrdinalIgnoreCase))
        {
            events.Add(new MonitoringEvent
            {
                OccurredAtUtc = sample.CapturedAtUtc,
                Kind = MonitoringEventKind.AdapterChanged,
                PreviousState = previousState,
                NewState = sample.State,
                Detail = sample.AdapterName
            });
        }

        if (previousGateway is not null
            && !string.Equals(previousGateway, sample.Gateway, StringComparison.OrdinalIgnoreCase))
        {
            events.Add(new MonitoringEvent
            {
                OccurredAtUtc = sample.CapturedAtUtc,
                Kind = MonitoringEventKind.GatewayChanged,
                PreviousState = previousState,
                NewState = sample.State,
                Detail = sample.Gateway
            });
        }

        return events;
    }

    private static MonitoringSummary CalculateSummary(
        IReadOnlyList<MonitoringSample> samples,
        IReadOnlyList<MonitoringEvent> events,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset? openOutageStartedAtUtc)
    {
        if (samples.Count == 0)
        {
            return new MonitoringSummary();
        }

        var successful = samples.Where(sample => sample.State != ConnectionState.Offline).ToArray();
        var lostPings = samples.Count(sample => sample.PreferredLatencyMilliseconds is null);
        var latencies = samples
            .Select(sample => sample.PreferredLatencyMilliseconds)
            .OfType<double>()
            .ToArray();
        var completedOutageDuration = events
            .Where(item => item.Kind == MonitoringEventKind.ConnectionRecovered
                || item.Kind == MonitoringEventKind.OutageStarted)
            .Sum(item => item.Duration?.TotalMilliseconds ?? 0);
        var openOutageDuration = openOutageStartedAtUtc is null
            ? 0
            : Math.Max(0, (capturedAtUtc - openOutageStartedAtUtc.Value).TotalMilliseconds);

        return new MonitoringSummary
        {
            TotalSamples = samples.Count,
            SuccessfulSamples = successful.Length,
            OfflineSamples = samples.Count - successful.Length,
            OutageCount = events.Count(item => item.Kind == MonitoringEventKind.OutageStarted),
            AvailabilityPercent = successful.Length * 100d / samples.Count,
            PacketLossPercent = lostPings * 100d / samples.Count,
            AverageLatencyMilliseconds = latencies.Length == 0 ? 0 : latencies.Average(),
            MaximumLatencyMilliseconds = latencies.Length == 0 ? 0 : latencies.Max(),
            AverageJitterMilliseconds = samples.Average(sample => sample.JitterMilliseconds),
            TotalOutageDuration = TimeSpan.FromMilliseconds(completedOutageDuration + openOutageDuration),
            Ipv4Available = samples.Any(sample => sample.Ipv4LatencyMilliseconds is not null),
            Ipv6Available = samples.Any(sample => sample.Ipv6LatencyMilliseconds is not null)
        };
    }

    private static IReadOnlyList<WindowsNetworkEvent> CorrelateWindowsEvents(
        IReadOnlyList<WindowsNetworkEvent> windowsEvents,
        IReadOnlyList<MonitoringEvent> monitoringEvents)
    {
        var significantEvents = monitoringEvents
            .Where(item => item.Kind is MonitoringEventKind.OutageStarted
                or MonitoringEventKind.ConnectionRecovered)
            .ToArray();
        return windowsEvents.Select(item =>
        {
            var nearest = significantEvents
                .Select(candidate => new
                {
                    Event = candidate,
                    Distance = (candidate.OccurredAtUtc - item.OccurredAtUtc).Duration()
                })
                .Where(candidate => candidate.Distance <= TimeSpan.FromMinutes(2))
                .OrderBy(candidate => candidate.Distance)
                .FirstOrDefault();
            return nearest is null ? item : item with { RelatedMonitoringEventId = nearest.Event.Id };
        }).ToArray();
    }
}
