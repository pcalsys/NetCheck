using System.IO;
using NetCheck.App.Localization;
using NetCheck.App.Services;
using NetCheck.App.ViewModels;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Logging;

namespace NetCheck.App.Tests;

public sealed class MonitoringViewModelTests
{
    [Fact]
    public async Task StartCommand_PersistsSessionBuildsBaselineAndRaisesOutageNotification()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var outageEvent = new MonitoringEvent
        {
            OccurredAtUtc = timestamp,
            Kind = MonitoringEventKind.OutageStarted,
            NewState = ConnectionState.Offline
        };
        var session = Session(timestamp, wasStopped: false, availability: 80, [outageEvent]);
        var service = new CompletingMonitoringService(session, outageEvent);
        var history = new MemoryMonitoringStore
        {
            Existing = [Session(timestamp.AddHours(-1), wasStopped: false, availability: 95, [])]
        };
        var notifications = new CollectingNotificationService();
        var viewModel = CreateViewModel(service, history, notifications);

        await viewModel.StartCommand.ExecuteAsync();

        Assert.False(viewModel.IsRunning);
        Assert.NotNull(history.Saved);
        Assert.Equal(BaselineTrend.Worse, history.Saved.Baseline.Trend);
        Assert.Equal("80.0%", viewModel.AvailabilityText);
        Assert.Contains(notifications.Items, item => item.Kind == NotificationKind.Warning);
        Assert.Contains("saved", viewModel.OperationStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StopCommand_CancelsAndSafelySavesPartialSession()
    {
        var service = new CancellationAwareMonitoringService();
        var history = new MemoryMonitoringStore();
        var viewModel = CreateViewModel(service, history, new CollectingNotificationService());

        var run = viewModel.StartCommand.ExecuteAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        viewModel.StopCommand.Execute(null);
        await run;

        Assert.False(viewModel.IsRunning);
        Assert.True(history.Saved?.WasStopped);
        Assert.Contains("partial", viewModel.OperationStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static MonitoringViewModel CreateViewModel(
        IMonitoringService service,
        IMonitoringHistoryStore history,
        INotificationService notifications)
    {
        var log = Path.Combine(Path.GetTempPath(), $"NetCheck-{Guid.NewGuid():N}.log");
        return new MonitoringViewModel(
            service,
            history,
            new StubSupportBundleService(),
            new StubUpdateService(),
            new StubFileDialogService(),
            new StubMessageService(),
            notifications,
            new LocalizationService(),
            new FileLogger(log));
    }

    private static MonitoringSession Session(
        DateTimeOffset timestamp,
        bool wasStopped,
        double availability,
        IReadOnlyList<MonitoringEvent> events) => new()
        {
            StartedAtUtc = timestamp.AddMinutes(-1),
            CompletedAtUtc = timestamp,
            WasStopped = wasStopped,
            Profile = MonitoringProfile.Standard,
            RunLength = MonitoringRunLength.FifteenMinutes,
            Samples =
        [
            new MonitoringSample
            {
                CapturedAtUtc = timestamp,
                State = availability >= 99 ? ConnectionState.Online : ConnectionState.Offline,
                PreferredLatencyMilliseconds = 50
            }
        ],
            Events = events,
            Summary = new MonitoringSummary
            {
                TotalSamples = 10,
                SuccessfulSamples = (int)(availability / 10),
                AvailabilityPercent = availability,
                PacketLossPercent = 100 - availability,
                AverageLatencyMilliseconds = availability >= 90 ? 30 : 70
            }
        };

    private sealed class CompletingMonitoringService(
        MonitoringSession session,
        MonitoringEvent outageEvent) : IMonitoringService
    {
        public async Task<MonitoringSession> RunAsync(
            MonitoringOptions options,
            IProgress<MonitoringProgress>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(new MonitoringProgress
            {
                LatestSample = session.Samples[0],
                NewEvents = [outageEvent],
                Summary = session.Summary,
                Environment = new NetworkEnvironmentSnapshot(),
                Elapsed = TimeSpan.FromSeconds(2),
                TargetDuration = options.Duration
            });
            await Task.Delay(30, cancellationToken);
            return session;
        }
    }

    private sealed class CancellationAwareMonitoringService : IMonitoringService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<MonitoringSession> RunAsync(
            MonitoringOptions options,
            IProgress<MonitoringProgress>? progress,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Session(DateTimeOffset.UtcNow, wasStopped: true, availability: 100, []);
            }

            throw new InvalidOperationException("The monitoring cancellation wait unexpectedly completed.");
        }
    }

    private sealed class MemoryMonitoringStore : IMonitoringHistoryStore
    {
        public IReadOnlyList<MonitoringSession> Existing { get; init; } = Array.Empty<MonitoringSession>();

        public MonitoringSession? Saved { get; private set; }

        public Task SaveAsync(MonitoringSession session, CancellationToken cancellationToken = default)
        {
            Saved = session;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MonitoringSession>> GetRecentAsync(
            int maximumCount = 100,
            CancellationToken cancellationToken = default) => Task.FromResult(Existing);

        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CollectingNotificationService : INotificationService
    {
        public event EventHandler<AppNotification>? NotificationRaised;

        public List<AppNotification> Items { get; } = [];

        public void Show(string title, string message, NotificationKind kind)
        {
            var notification = new AppNotification(title, message, kind, DateTimeOffset.UtcNow);
            Items.Add(notification);
            NotificationRaised?.Invoke(this, notification);
        }
    }

    private sealed class StubSupportBundleService : ISupportBundleService
    {
        public Task CreateAsync(string destinationPath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubUpdateService : IUpdateService
    {
        public Task<UpdateCheckResult> CheckAsync(
            Version currentVersion,
            CancellationToken cancellationToken = default) => Task.FromResult(new UpdateCheckResult
            {
                CurrentVersion = currentVersion,
                LatestVersion = currentVersion,
                ReleasePageUri = new Uri("https://github.com/pcalsys/NetCheck/releases")
            });
    }

    private sealed class StubFileDialogService : IFileDialogService
    {
        public string? ShowReportSaveDialog(string suggestedFileName) => null;
    }

    private sealed class StubMessageService : IMessageService
    {
        public void ShowError(string title, string message)
        {
        }

        public void ShowInformation(string title, string message)
        {
        }

        public bool Confirm(string title, string message) => true;
    }
}
