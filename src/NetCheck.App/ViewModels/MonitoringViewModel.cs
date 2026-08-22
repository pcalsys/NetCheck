using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using NetCheck.App.Localization;
using NetCheck.App.Mvvm;
using NetCheck.App.Services;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;
using NetCheck.Core.Monitoring;
using NetCheck.Infrastructure.Logging;

namespace NetCheck.App.ViewModels;

public sealed class MonitoringViewModel : ObservableObject
{
    private const int MaximumChartSamples = 240;
    private const int MaximumVisibleEvents = 100;
    private readonly IMonitoringService _monitoringService;
    private readonly IMonitoringHistoryStore _historyStore;
    private readonly ISupportBundleService _supportBundleService;
    private readonly IUpdateService _updateService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IMessageService _messageService;
    private readonly INotificationService _notificationService;
    private readonly LocalizationService _text;
    private readonly FileLogger _logger;
    private CancellationTokenSource? _monitoringCancellation;
    private Task? _activeMonitoringTask;
    private MonitoringChoice<MonitoringProfile>? _selectedProfile;
    private MonitoringChoice<MonitoringRunLength>? _selectedRunLength;
    private MonitoringSession? _lastSession;
    private NetworkEnvironmentSnapshot _environment = new();
    private MonitoringSummary _summary = new();
    private ConnectionState _currentState = ConnectionState.Unknown;
    private TimeSpan _elapsed;
    private TimeSpan? _targetDuration;
    private double? _latestLatency;
    private double _latestJitter;
    private double _latestPacketLoss;
    private DateTimeOffset? _latestCapturedAtUtc;
    private bool _isRunning;
    private string _operationStatus = string.Empty;
    private string _updateStatus = string.Empty;
    private string _operationStatusSource = "Ready to monitor.";
    private string _updateStatusSource = "Updates are checked only when you request it.";
    private object?[] _operationStatusArguments = [];
    private object?[] _updateStatusArguments = [];
    private UpdateCheckResult? _lastUpdateResult;
    private Uri? _releasePageUri;

    public MonitoringViewModel(
        IMonitoringService monitoringService,
        IMonitoringHistoryStore historyStore,
        ISupportBundleService supportBundleService,
        IUpdateService updateService,
        IFileDialogService fileDialogService,
        IMessageService messageService,
        INotificationService notificationService,
        LocalizationService text,
        FileLogger logger)
    {
        _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
        _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
        _supportBundleService = supportBundleService ?? throw new ArgumentNullException(nameof(supportBundleService));
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RebuildChoices(MonitoringProfile.Standard, MonitoringRunLength.FifteenMinutes);
        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning);
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        CreateSupportBundleCommand = new AsyncRelayCommand(CreateSupportBundleAsync);
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
        OpenReleaseCommand = new RelayCommand(OpenRelease, () => ReleasePageUri is not null);
        StartCommand.ExecutionFailed += OnCommandFailed;
        CreateSupportBundleCommand.ExecutionFailed += OnCommandFailed;
        CheckForUpdatesCommand.ExecutionFailed += OnCommandFailed;
        SetOperationStatus("Ready to monitor.");
        SetUpdateStatus("Updates are checked only when you request it.");
    }

    public ObservableCollection<MonitoringChoice<MonitoringProfile>> ProfileChoices { get; } = [];

    public ObservableCollection<MonitoringChoice<MonitoringRunLength>> RunLengthChoices { get; } = [];

    public ObservableCollection<double?> LatencyValues { get; } = [];

    public ObservableCollection<double?> JitterValues { get; } = [];

    public ObservableCollection<double?> PacketLossValues { get; } = [];

    public ObservableCollection<MonitoringEventItemViewModel> Events { get; } = [];

    public ObservableCollection<WindowsNetworkEventItemViewModel> WindowsEvents { get; } = [];

    public AsyncRelayCommand StartCommand { get; }

    public RelayCommand StopCommand { get; }

    public AsyncRelayCommand CreateSupportBundleCommand { get; }

    public AsyncRelayCommand CheckForUpdatesCommand { get; }

    public RelayCommand OpenReleaseCommand { get; }

    public MonitoringChoice<MonitoringProfile>? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public MonitoringChoice<MonitoringRunLength>? SelectedRunLength
    {
        get => _selectedRunLength;
        set => SetProperty(ref _selectedRunLength, value);
    }

    public MonitoringSession? LastSession
    {
        get => _lastSession;
        private set
        {
            if (SetProperty(ref _lastSession, value))
            {
                OnPropertiesChanged(nameof(HasBaseline), nameof(BaselineText), nameof(BaselineDetailText));
            }
        }
    }

    public NetworkEnvironmentSnapshot Environment
    {
        get => _environment;
        private set
        {
            if (SetProperty(ref _environment, value))
            {
                OnPropertiesChanged(
                    nameof(AdapterText),
                    nameof(DriverText),
                    nameof(IpSupportText),
                    nameof(WifiText),
                    nameof(WifiRatesText),
                    nameof(VpnText),
                    nameof(FirewallText),
                    nameof(Ipv4RouteText),
                    nameof(Ipv6RouteText),
                    nameof(EnvironmentIssueText),
                    nameof(HasEnvironmentIssues));
            }
        }
    }

    public MonitoringSummary Summary
    {
        get => _summary;
        private set
        {
            if (SetProperty(ref _summary, value))
            {
                OnPropertiesChanged(
                    nameof(AvailabilityText),
                    nameof(OutageCountText),
                    nameof(OutageDurationText),
                    nameof(AverageLatencyText),
                    nameof(MaximumLatencyText),
                    nameof(AverageJitterText),
                    nameof(IpAvailabilityText));
            }
        }
    }

    public ConnectionState CurrentState
    {
        get => _currentState;
        private set
        {
            if (SetProperty(ref _currentState, value))
            {
                OnPropertiesChanged(nameof(ConnectionStatusText), nameof(ConnectionStatusDetail));
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetProperty(ref _isRunning, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsIdle));
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsIdle => !IsRunning;

    public bool HasEvents => Events.Count > 0;

    public bool HasWindowsEvents => WindowsEvents.Count > 0;

    public string OperationStatus
    {
        get => _operationStatus;
        private set => SetProperty(ref _operationStatus, value);
    }

    public string UpdateStatus
    {
        get => _updateStatus;
        private set => SetProperty(ref _updateStatus, value);
    }

    public Uri? ReleasePageUri
    {
        get => _releasePageUri;
        private set
        {
            if (SetProperty(ref _releasePageUri, value))
            {
                OnPropertyChanged(nameof(HasReleasePage));
                OpenReleaseCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasReleasePage => ReleasePageUri is not null;

    public string ConnectionStatusText => CurrentState switch
    {
        ConnectionState.Online => _text.Translate("Online"),
        ConnectionState.Degraded => _text.Translate("Degraded"),
        ConnectionState.Offline => _text.Translate("Offline"),
        _ => _text.Translate("Waiting")
    };

    public string ConnectionStatusDetail => CurrentState switch
    {
        ConnectionState.Online => _text.Translate("The current checks are healthy."),
        ConnectionState.Degraded => _text.Translate("The connection works, but quality is below this profile's thresholds."),
        ConnectionState.Offline => _text.Translate("No usable internet path is currently responding."),
        _ => _text.Translate("Start monitoring to collect live quality data.")
    };

    public string ElapsedText => FormatDuration(_elapsed);

    public string RemainingText => _targetDuration is null
        ? _text.Translate("Continuous")
        : FormatDuration(_targetDuration.Value - _elapsed < TimeSpan.Zero
            ? TimeSpan.Zero
            : _targetDuration.Value - _elapsed);

    public string LatestLatencyText => FormatMilliseconds(_latestLatency);

    public string LatestJitterText => FormatMilliseconds(_latestJitter);

    public string LatestPacketLossText => FormatPercent(_latestPacketLoss);

    public string LatestCapturedText => _latestCapturedAtUtc is null
        ? "—"
        : _latestCapturedAtUtc.Value.ToLocalTime().ToString("T", _text.Culture);

    public string AvailabilityText => FormatPercent(Summary.AvailabilityPercent);

    public string OutageCountText => Summary.OutageCount.ToString("N0", _text.Culture);

    public string OutageDurationText => FormatDuration(Summary.TotalOutageDuration);

    public string AverageLatencyText => FormatMilliseconds(Summary.AverageLatencyMilliseconds);

    public string MaximumLatencyText => FormatMilliseconds(Summary.MaximumLatencyMilliseconds);

    public string AverageJitterText => FormatMilliseconds(Summary.AverageJitterMilliseconds);

    public string IpAvailabilityText => _text.Format(
        "IPv4 {0} · IPv6 {1}",
        _text.Translate(Summary.Ipv4Available ? "available" : "unavailable"),
        _text.Translate(Summary.Ipv6Available ? "available" : "unavailable"));

    public string AdapterText => string.IsNullOrWhiteSpace(Environment.AdapterName)
        ? "—"
        : $"{Environment.AdapterName} · {Environment.InterfaceType}";

    public string DriverText => string.IsNullOrWhiteSpace(Environment.DriverVersion)
        ? _text.Translate("Driver version unavailable")
        : _text.Format("Driver {0}", Environment.DriverVersion);

    public string IpSupportText => _text.Format(
        "IPv4 {0} · IPv6 {1}",
        _text.Translate(Environment.SupportsIpv4 ? "supported" : "not supported"),
        _text.Translate(Environment.SupportsIpv6 ? "supported" : "not supported"));

    public string WifiText => Environment.Wifi is not { IsConnected: true } wifi
        ? _text.Translate("No connected Wi-Fi interface")
        : _text.Format(
            "{0} · {1}% · channel {2} · {3}",
            wifi.Ssid,
            wifi.SignalQualityPercent?.ToString(CultureInfo.InvariantCulture) ?? "—",
            wifi.Channel?.ToString(CultureInfo.InvariantCulture) ?? "—",
            string.IsNullOrWhiteSpace(wifi.Band) ? "—" : wifi.Band);

    public string WifiRatesText => Environment.Wifi is not { IsConnected: true } wifi
        ? "—"
        : _text.Format(
            "Receive {0} Mbit/s · transmit {1} Mbit/s · {2}",
            FormatNumber(wifi.ReceiveRateMegabitsPerSecond),
            FormatNumber(wifi.TransmitRateMegabitsPerSecond),
            string.IsNullOrWhiteSpace(wifi.RadioType) ? "—" : wifi.RadioType);

    public string VpnText => Environment.VpnAdapters.Count == 0
        ? _text.Translate("No VPN adapter detected")
        : string.Join(", ", Environment.VpnAdapters);

    public string FirewallText => _text.Format(
        "Domain {0} · Private {1} · Public {2}",
        FormatEnabled(Environment.Firewall.DomainEnabled),
        FormatEnabled(Environment.Firewall.PrivateEnabled),
        FormatEnabled(Environment.Firewall.PublicEnabled));

    public string Ipv4RouteText => FormatRoute(Environment.Ipv4Route);

    public string Ipv6RouteText => FormatRoute(Environment.Ipv6Route);

    public bool HasEnvironmentIssues => Environment.Errors.Count > 0;

    public string EnvironmentIssueText => string.Join(System.Environment.NewLine, Environment.Errors);

    public bool HasBaseline => LastSession?.Baseline.Trend is not null and not BaselineTrend.NoBaseline;

    public string BaselineText => LastSession?.Baseline.Trend switch
    {
        BaselineTrend.Better => _text.Translate("Better than your local baseline"),
        BaselineTrend.Worse => _text.Translate("Worse than your local baseline"),
        BaselineTrend.Similar => _text.Translate("Similar to your local baseline"),
        _ => _text.Translate("A baseline appears after another session with this profile.")
    };

    public string BaselineDetailText => LastSession is not { } session
        || session.Baseline.Trend == BaselineTrend.NoBaseline
        ? string.Empty
        : _text.Format(
            "Compared with {0} sessions: latency {1}; packet loss {2}; availability {3}.",
            session.Baseline.ComparedSessionCount,
            FormatSignedPercent(session.Baseline.LatencyDifferencePercent),
            FormatSignedPoints(session.Baseline.PacketLossDifferencePercentagePoints),
            FormatSignedPoints(session.Baseline.AvailabilityDifferencePercentagePoints));

    public void RefreshLocalization()
    {
        var selectedProfile = SelectedProfile?.Value ?? MonitoringProfile.Standard;
        var selectedRunLength = SelectedRunLength?.Value ?? MonitoringRunLength.FifteenMinutes;
        RebuildChoices(selectedProfile, selectedRunLength);
        RebuildEvents(LastSession?.Events ?? Events.Select(item => item.Source).ToArray());
        RebuildWindowsEvents(LastSession?.WindowsEvents
            ?? WindowsEvents.Select(item => item.Source).ToArray());
        SetOperationStatus(_operationStatusSource, _operationStatusArguments);
        if (_lastUpdateResult is not null)
        {
            ApplyUpdateResult(_lastUpdateResult);
        }
        else
        {
            SetUpdateStatus(_updateStatusSource, _updateStatusArguments);
        }
        OnPropertiesChanged(
            nameof(ConnectionStatusText),
            nameof(ConnectionStatusDetail),
            nameof(ElapsedText),
            nameof(RemainingText),
            nameof(LatestLatencyText),
            nameof(LatestJitterText),
            nameof(LatestPacketLossText),
            nameof(LatestCapturedText),
            nameof(AvailabilityText),
            nameof(OutageCountText),
            nameof(OutageDurationText),
            nameof(AverageLatencyText),
            nameof(MaximumLatencyText),
            nameof(AverageJitterText),
            nameof(IpAvailabilityText),
            nameof(AdapterText),
            nameof(DriverText),
            nameof(IpSupportText),
            nameof(WifiText),
            nameof(WifiRatesText),
            nameof(VpnText),
            nameof(FirewallText),
            nameof(Ipv4RouteText),
            nameof(Ipv6RouteText),
            nameof(BaselineText),
            nameof(BaselineDetailText));
    }

    public async Task ShutdownAsync()
    {
        _monitoringCancellation?.Cancel();
        if (_activeMonitoringTask is not null)
        {
            await _activeMonitoringTask.ConfigureAwait(false);
        }
    }

    private async Task StartAsync()
    {
        if (IsRunning || SelectedProfile is null || SelectedRunLength is null)
        {
            return;
        }

        ResetLiveState();
        var options = MonitoringOptions.Create(SelectedProfile.Value, SelectedRunLength.Value);
        _targetDuration = options.Duration;
        _monitoringCancellation = new CancellationTokenSource();
        IsRunning = true;
        SetOperationStatus("Monitoring is running. You can change pages without interrupting it.");
        var progress = new Progress<MonitoringProgress>(ApplyProgress);
        _activeMonitoringTask = RunAndPersistAsync(options, progress, _monitoringCancellation.Token);
        try
        {
            await _activeMonitoringTask.ConfigureAwait(true);
        }
        finally
        {
            _activeMonitoringTask = null;
            _monitoringCancellation.Dispose();
            _monitoringCancellation = null;
            IsRunning = false;
        }
    }

    private async Task RunAndPersistAsync(
        MonitoringOptions options,
        IProgress<MonitoringProgress> progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _monitoringService.RunAsync(options, progress, cancellationToken)
                .ConfigureAwait(true);
            IReadOnlyList<MonitoringSession> previousSessions;
            try
            {
                previousSessions = await _historyStore.GetRecentAsync(100).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.Error("Could not load the monitoring baseline.", exception);
                previousSessions = Array.Empty<MonitoringSession>();
            }

            session = session with
            {
                Baseline = MonitoringBaselineCalculator.Compare(session, previousSessions)
            };
            await _historyStore.SaveAsync(session).ConfigureAwait(true);
            LastSession = session;
            Summary = session.Summary;
            Environment = session.FinalEnvironment ?? session.Environment;
            CurrentState = session.Samples.LastOrDefault()?.State ?? ConnectionState.Unknown;
            RebuildEvents(session.Events);
            RebuildWindowsEvents(session.WindowsEvents);
            SetOperationStatus(session.WasStopped
                ? "Monitoring stopped. The partial session was saved safely."
                : "Monitoring completed and was saved to local history.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error("Monitoring failed.", exception);
            SetOperationStatus("Monitoring could not continue. Completed checks were kept when possible.");
            _messageService.ShowError(_text.Translate("Monitoring error"), exception.Message);
        }
    }

    private void Stop() => _monitoringCancellation?.Cancel();

    private void ApplyProgress(MonitoringProgress progress)
    {
        var sample = progress.LatestSample;
        CurrentState = sample.State;
        Summary = progress.Summary;
        Environment = progress.Environment;
        _elapsed = progress.Elapsed;
        _targetDuration = progress.TargetDuration;
        _latestLatency = sample.PreferredLatencyMilliseconds;
        _latestJitter = sample.JitterMilliseconds;
        _latestPacketLoss = sample.RollingPacketLossPercent;
        _latestCapturedAtUtc = sample.CapturedAtUtc;
        AddChartValue(LatencyValues, sample.PreferredLatencyMilliseconds);
        AddChartValue(JitterValues, sample.JitterMilliseconds);
        AddChartValue(PacketLossValues, sample.RollingPacketLossPercent);
        foreach (var item in progress.NewEvents.Reverse())
        {
            Events.Insert(0, MonitoringEventItemViewModel.Create(item, _text));
            if (Events.Count > MaximumVisibleEvents)
            {
                Events.RemoveAt(Events.Count - 1);
            }

            OnPropertyChanged(nameof(HasEvents));

            NotifyConnectionTransition(item);
        }

        OnPropertiesChanged(
            nameof(ElapsedText),
            nameof(RemainingText),
            nameof(LatestLatencyText),
            nameof(LatestJitterText),
            nameof(LatestPacketLossText),
            nameof(LatestCapturedText));
    }

    private void NotifyConnectionTransition(MonitoringEvent item)
    {
        if (item.Kind == MonitoringEventKind.OutageStarted)
        {
            _notificationService.Show(
                _text.Translate("Connection lost"),
                _text.Format("NetCheck detected an outage at {0}.", item.OccurredAtUtc.ToLocalTime().ToString("T", _text.Culture)),
                NotificationKind.Warning);
        }
        else if (item.Kind == MonitoringEventKind.ConnectionRecovered)
        {
            _notificationService.Show(
                _text.Translate("Connection restored"),
                _text.Format("Connectivity returned at {0}.", item.OccurredAtUtc.ToLocalTime().ToString("T", _text.Culture)),
                NotificationKind.Success);
        }
    }

    private async Task CreateSupportBundleAsync()
    {
        var destination = _fileDialogService.ShowSupportBundleSaveDialog(
            $"NetCheck-Support-{DateTime.Now:yyyyMMdd-HHmm}.zip");
        if (destination is null)
        {
            return;
        }

        await _supportBundleService.CreateAsync(destination).ConfigureAwait(true);
        _messageService.ShowInformation(
            _text.Translate("Support bundle created"),
            _text.Format("The anonymized support bundle was saved to:\n{0}", destination));
    }

    private async Task CheckForUpdatesAsync()
    {
        ReleasePageUri = null;
        SetUpdateStatus("Checking the official GitHub release…");
        var result = await _updateService.CheckAsync(GetCurrentVersion()).ConfigureAwait(true);
        _lastUpdateResult = result;
        ReleasePageUri = result.ReleasePageUri;
        ApplyUpdateResult(result);
    }

    private void ApplyUpdateResult(UpdateCheckResult result)
    {
        if (result.UpdateAvailable)
        {
            SetUpdateStatus(
                result.HasVerifiedReleaseAssets
                    ? "Version {0} is available with both ZIP and SHA-256 assets."
                    : "Version {0} is available, but its release asset pair is incomplete.",
                result.LatestVersion.ToString(3));
        }
        else
        {
            SetUpdateStatus("NetCheck {0} is up to date.", result.CurrentVersion.ToString(3));
        }
    }

    private void SetOperationStatus(string source, params object?[] arguments)
    {
        _operationStatusSource = source;
        _operationStatusArguments = arguments;
        OperationStatus = arguments.Length == 0
            ? _text.Translate(source)
            : _text.Format(source, arguments);
    }

    private void SetUpdateStatus(string source, params object?[] arguments)
    {
        _updateStatusSource = source;
        _updateStatusArguments = arguments;
        UpdateStatus = arguments.Length == 0
            ? _text.Translate(source)
            : _text.Format(source, arguments);
    }

    private void OpenRelease()
    {
        if (ReleasePageUri is not { Scheme: "https", Host: "github.com" } uri
            || !uri.AbsolutePath.StartsWith("/pcalsys/NetCheck/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private void ResetLiveState()
    {
        LastSession = null;
        Summary = new MonitoringSummary();
        Environment = new NetworkEnvironmentSnapshot();
        CurrentState = ConnectionState.Unknown;
        _elapsed = TimeSpan.Zero;
        _latestLatency = null;
        _latestJitter = 0;
        _latestPacketLoss = 0;
        _latestCapturedAtUtc = null;
        LatencyValues.Clear();
        JitterValues.Clear();
        PacketLossValues.Clear();
        Events.Clear();
        WindowsEvents.Clear();
        OnPropertyChanged(nameof(HasEvents));
        OnPropertyChanged(nameof(HasWindowsEvents));
        OnPropertiesChanged(
            nameof(ElapsedText),
            nameof(RemainingText),
            nameof(LatestLatencyText),
            nameof(LatestJitterText),
            nameof(LatestPacketLossText),
            nameof(LatestCapturedText));
    }

    private void RebuildChoices(
        MonitoringProfile selectedProfile,
        MonitoringRunLength selectedRunLength)
    {
        ProfileChoices.Clear();
        ProfileChoices.Add(new(MonitoringProfile.Standard, _text.Translate("Standard")));
        ProfileChoices.Add(new(MonitoringProfile.Gaming, _text.Translate("Gaming")));
        ProfileChoices.Add(new(MonitoringProfile.Streaming, _text.Translate("Streaming")));
        ProfileChoices.Add(new(MonitoringProfile.HomeOffice, _text.Translate("Home office")));
        RunLengthChoices.Clear();
        RunLengthChoices.Add(new(MonitoringRunLength.FifteenMinutes, _text.Translate("15 minutes")));
        RunLengthChoices.Add(new(MonitoringRunLength.ThirtyMinutes, _text.Translate("30 minutes")));
        RunLengthChoices.Add(new(MonitoringRunLength.SixtyMinutes, _text.Translate("60 minutes")));
        RunLengthChoices.Add(new(MonitoringRunLength.Continuous, _text.Translate("Continuous")));
        SelectedProfile = ProfileChoices.First(item => item.Value == selectedProfile);
        SelectedRunLength = RunLengthChoices.First(item => item.Value == selectedRunLength);
    }

    private void RebuildEvents(IEnumerable<MonitoringEvent> source)
    {
        Events.Clear();
        foreach (var item in source.OrderByDescending(item => item.OccurredAtUtc).Take(MaximumVisibleEvents))
        {
            Events.Add(MonitoringEventItemViewModel.Create(item, _text));
        }

        OnPropertyChanged(nameof(HasEvents));
    }

    private void RebuildWindowsEvents(IEnumerable<WindowsNetworkEvent> source)
    {
        WindowsEvents.Clear();
        foreach (var item in source
                     .Where(item => item.RelatedMonitoringEventId is not null)
                     .OrderByDescending(item => item.OccurredAtUtc)
                     .Take(30))
        {
            WindowsEvents.Add(WindowsNetworkEventItemViewModel.Create(item, _text));
        }

        OnPropertyChanged(nameof(HasWindowsEvents));
    }

    private static void AddChartValue(ObservableCollection<double?> collection, double? value)
    {
        collection.Add(value);
        if (collection.Count > MaximumChartSamples)
        {
            collection.RemoveAt(0);
        }
    }

    private string FormatDuration(TimeSpan value) => value.TotalHours >= 1
        ? value.ToString("h\\:mm\\:ss", CultureInfo.InvariantCulture)
        : value.ToString("mm\\:ss", CultureInfo.InvariantCulture);

    private string FormatMilliseconds(double? value) => value is null
        ? "—"
        : string.Format(_text.Culture, "{0:N1} ms", value.Value);

    private string FormatPercent(double value) =>
        string.Format(_text.Culture, "{0:N1}%", value);

    private string FormatNumber(double? value) => value is null
        ? "—"
        : value.Value.ToString("N1", _text.Culture);

    private string FormatEnabled(bool value) => _text.Translate(value ? "on" : "off");

    private string FormatRoute(IReadOnlyList<RouteHop> route) => route.Count == 0
        ? _text.Translate("Route unavailable")
        : string.Join("  →  ", route.Select(hop => $"{hop.Hop}: {hop.Address}"));

    private string FormatSignedPercent(double value) =>
        string.Format(_text.Culture, "{0:+0.0;-0.0;0.0}%", value);

    private string FormatSignedPoints(double value) =>
        string.Format(_text.Culture, "{0:+0.0;-0.0;0.0} pp", value);

    private static Version GetCurrentVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version
        ?? typeof(MonitoringViewModel).Assembly.GetName().Version
        ?? new Version(1, 2, 0);

    private void OnCommandFailed(object? sender, Exception exception)
    {
        _logger.Error("Monitoring page operation failed.", exception);
        _messageService.ShowError(_text.Translate("Operation failed"), exception.Message);
    }
}

public sealed record MonitoringChoice<T>(T Value, string Label);

public sealed record MonitoringEventItemViewModel(
    MonitoringEvent Source,
    string Title,
    string Detail,
    string TimeText,
    bool IsOutage,
    bool IsRecovery)
{
    public static MonitoringEventItemViewModel Create(
        MonitoringEvent source,
        LocalizationService text)
    {
        var title = source.Kind switch
        {
            MonitoringEventKind.SessionStarted => text.Translate("Monitoring started"),
            MonitoringEventKind.SessionCompleted => text.Translate("Monitoring completed"),
            MonitoringEventKind.SessionStopped => text.Translate("Monitoring stopped"),
            MonitoringEventKind.OutageStarted => text.Translate("Connection lost"),
            MonitoringEventKind.ConnectionRecovered => text.Translate("Connection restored"),
            MonitoringEventKind.QualityDegraded => text.Translate("Quality degraded"),
            MonitoringEventKind.QualityRecovered => text.Translate("Quality recovered"),
            MonitoringEventKind.AdapterChanged => text.Translate("Network adapter changed"),
            MonitoringEventKind.GatewayChanged => text.Translate("Gateway changed"),
            MonitoringEventKind.ProbeIssue => text.Translate("A check failed without stopping monitoring"),
            _ => source.Kind.ToString()
        };
        var detail = source.Duration is { } duration
            ? text.Format("Duration: {0}", duration.TotalHours >= 1
                ? duration.ToString("h\\:mm\\:ss", CultureInfo.InvariantCulture)
                : duration.ToString("mm\\:ss", CultureInfo.InvariantCulture))
            : source.Detail;
        return new MonitoringEventItemViewModel(
            source,
            title,
            detail,
            source.OccurredAtUtc.ToLocalTime().ToString("T", text.Culture),
            source.Kind == MonitoringEventKind.OutageStarted,
            source.Kind == MonitoringEventKind.ConnectionRecovered);
    }
}

public sealed record WindowsNetworkEventItemViewModel(
    WindowsNetworkEvent Source,
    string Title,
    string Detail,
    string TimeText)
{
    public static WindowsNetworkEventItemViewModel Create(
        WindowsNetworkEvent source,
        LocalizationService text) => new(
            source,
            text.Format("{0} · event {1}", source.Provider, source.EventId),
            string.IsNullOrWhiteSpace(source.Detail)
                ? text.Translate("Windows recorded a related network event.")
                : source.Detail,
            source.OccurredAtUtc.ToLocalTime().ToString("T", text.Culture));
}
