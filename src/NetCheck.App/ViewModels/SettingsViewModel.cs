using System.Net;
using NetCheck.App.Mvvm;
using NetCheck.App.Services;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Logging;

namespace NetCheck.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly IMessageService _messageService;
    private readonly FileLogger _logger;
    private DiagnosticOptions _loaded = new();
    private string _dnsTestHost = string.Empty;
    private string _pingTargets = string.Empty;
    private string _connectivityUrl = string.Empty;
    private int _pingTimeoutMilliseconds;
    private int _stabilitySampleCount;
    private double _packetLossWarningPercent;
    private double _latencyWarningMilliseconds;
    private bool _autoRunOnLaunch;
    private bool _saveDiagnosticHistory;
    private bool _includeComputerNameInExports;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public SettingsViewModel(
        ISettingsStore settingsStore,
        IMessageService messageService,
        FileLogger logger)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        ResetCommand = new RelayCommand(ResetToDefaults, () => !IsBusy);
        SaveCommand.ExecutionFailed += (_, exception) =>
        {
            _logger.Error("Settings operation failed.", exception);
            _messageService.ShowError("Settings could not be saved", exception.Message);
        };
    }

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand ResetCommand { get; }

    public string DnsTestHost
    {
        get => _dnsTestHost;
        set => SetProperty(ref _dnsTestHost, value);
    }

    public string PingTargets
    {
        get => _pingTargets;
        set => SetProperty(ref _pingTargets, value);
    }

    public string ConnectivityUrl
    {
        get => _connectivityUrl;
        set => SetProperty(ref _connectivityUrl, value);
    }

    public int PingTimeoutMilliseconds
    {
        get => _pingTimeoutMilliseconds;
        set => SetProperty(ref _pingTimeoutMilliseconds, value);
    }

    public int StabilitySampleCount
    {
        get => _stabilitySampleCount;
        set => SetProperty(ref _stabilitySampleCount, value);
    }

    public double PacketLossWarningPercent
    {
        get => _packetLossWarningPercent;
        set => SetProperty(ref _packetLossWarningPercent, value);
    }

    public double LatencyWarningMilliseconds
    {
        get => _latencyWarningMilliseconds;
        set => SetProperty(ref _latencyWarningMilliseconds, value);
    }

    public bool AutoRunOnLaunch
    {
        get => _autoRunOnLaunch;
        set => SetProperty(ref _autoRunOnLaunch, value);
    }

    public bool SaveDiagnosticHistory
    {
        get => _saveDiagnosticHistory;
        set => SetProperty(ref _saveDiagnosticHistory, value);
    }

    public bool IncludeComputerNameInExports
    {
        get => _includeComputerNameInExports;
        set => SetProperty(ref _includeComputerNameInExports, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
                ResetCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            _loaded = await _settingsStore.LoadAsync().ConfigureAwait(true);
            Apply(_loaded);
            StatusMessage = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        StatusMessage = string.Empty;
        if (!TryBuildSettings(out var settings, out var error))
        {
            StatusMessage = error;
            return;
        }

        IsBusy = true;
        try
        {
            await _settingsStore.SaveAsync(settings!).ConfigureAwait(true);
            _loaded = settings!;
            Apply(_loaded);
            StatusMessage = "Settings saved. They will be used for the next diagnostic.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryBuildSettings(out DiagnosticOptions? settings, out string error)
    {
        settings = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(DnsTestHost) || DnsTestHost.Any(char.IsWhiteSpace))
        {
            error = "Enter a valid DNS test hostname without spaces.";
            return false;
        }

        var targets = PingTargets
            .Split([',', ';', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (targets.Length == 0 || targets.Any(target => !IPAddress.TryParse(target, out _)))
        {
            error = "Enter one or more valid IP addresses for internet ping targets.";
            return false;
        }

        if (!Uri.TryCreate(ConnectivityUrl, UriKind.Absolute, out var connectivityUri)
            || connectivityUri.Scheme is not ("http" or "https"))
        {
            error = "Enter a valid HTTP or HTTPS connectivity URL.";
            return false;
        }

        if (PingTimeoutMilliseconds is < 500 or > 5000)
        {
            error = "Ping timeout must be between 500 and 5000 milliseconds.";
            return false;
        }

        if (StabilitySampleCount is < 3 or > 20)
        {
            error = "Stability samples must be between 3 and 20.";
            return false;
        }

        if (PacketLossWarningPercent is < 1 or > 100)
        {
            error = "The packet-loss warning threshold must be between 1 and 100 percent.";
            return false;
        }

        if (LatencyWarningMilliseconds is < 10 or > 2000)
        {
            error = "The latency warning threshold must be between 10 and 2000 milliseconds.";
            return false;
        }

        settings = _loaded with
        {
            DnsTestHost = DnsTestHost.Trim(),
            InternetPingTargets = targets,
            ConnectivityCheckUri = connectivityUri,
            PingTimeoutMilliseconds = PingTimeoutMilliseconds,
            StabilitySampleCount = StabilitySampleCount,
            PacketLossWarningPercent = PacketLossWarningPercent,
            LatencyWarningMilliseconds = LatencyWarningMilliseconds,
            AutoRunOnLaunch = AutoRunOnLaunch,
            SaveDiagnosticHistory = SaveDiagnosticHistory,
            IncludeComputerNameInExports = IncludeComputerNameInExports
        };
        return true;
    }

    private void ResetToDefaults()
    {
        Apply(new DiagnosticOptions());
        StatusMessage = "Defaults restored in the form. Choose Save settings to apply them.";
    }

    private void Apply(DiagnosticOptions settings)
    {
        DnsTestHost = settings.DnsTestHost;
        PingTargets = string.Join(", ", settings.InternetPingTargets);
        ConnectivityUrl = settings.ConnectivityCheckUri.ToString();
        PingTimeoutMilliseconds = settings.PingTimeoutMilliseconds;
        StabilitySampleCount = settings.StabilitySampleCount;
        PacketLossWarningPercent = settings.PacketLossWarningPercent;
        LatencyWarningMilliseconds = settings.LatencyWarningMilliseconds;
        AutoRunOnLaunch = settings.AutoRunOnLaunch;
        SaveDiagnosticHistory = settings.SaveDiagnosticHistory;
        IncludeComputerNameInExports = settings.IncludeComputerNameInExports;
    }
}

