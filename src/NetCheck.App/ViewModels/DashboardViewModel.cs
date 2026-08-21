using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using NetCheck.App.Mvvm;
using NetCheck.App.Services;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Logging;

namespace NetCheck.App.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly IDiagnosticEngine _diagnosticEngine;
    private readonly IReportHistoryStore _historyStore;
    private readonly IReportExporter _reportExporter;
    private readonly ISettingsStore _settingsStore;
    private readonly IFileDialogService _fileDialogService;
    private readonly IMessageService _messageService;
    private readonly FileLogger _logger;
    private CancellationTokenSource? _runCancellation;
    private DiagnosticReport? _report;
    private DiagnosticOptions _settings = new();
    private bool _isRunning;
    private bool _isInitialized;
    private int _progressPercentage;
    private string _currentCheckName = string.Empty;

    public DashboardViewModel(
        IDiagnosticEngine diagnosticEngine,
        IReportHistoryStore historyStore,
        IReportExporter reportExporter,
        ISettingsStore settingsStore,
        IFileDialogService fileDialogService,
        IMessageService messageService,
        FileLogger logger)
    {
        _diagnosticEngine = diagnosticEngine ?? throw new ArgumentNullException(nameof(diagnosticEngine));
        _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
        _reportExporter = reportExporter ?? throw new ArgumentNullException(nameof(reportExporter));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsRunning);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => Report is not null && !IsRunning);
        CopySummaryCommand = new RelayCommand(CopySummary, () => Report is not null);
        AttachFailureHandler(RunCommand, "running diagnostics");
        AttachFailureHandler(ExportCommand, "exporting a report");
    }

    public ObservableCollection<DiagnosticCheckResult> Results { get; } = [];

    public AsyncRelayCommand RunCommand { get; }

    public RelayCommand CancelCommand { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public RelayCommand CopySummaryCommand { get; }

    public DiagnosticReport? Report
    {
        get => _report;
        private set
        {
            if (!SetProperty(ref _report, value))
            {
                return;
            }

            OnPropertiesChanged(
                nameof(HasReport),
                nameof(StatusOutcome),
                nameof(StatusTitle),
                nameof(StatusSummary),
                nameof(PrimaryAdapterName),
                nameof(PrimaryIpAddress),
                nameof(PrimaryGateway),
                nameof(PrimaryDnsServer),
                nameof(CompletedText),
                nameof(HasRecommendations));
            ExportCommand.RaiseCanExecuteChanged();
            CopySummaryCommand.RaiseCanExecuteChanged();
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

            OnPropertiesChanged(nameof(StatusTitle), nameof(StatusSummary));
            RunCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
        }
    }

    public int ProgressPercentage
    {
        get => _progressPercentage;
        private set => SetProperty(ref _progressPercentage, value);
    }

    public string CurrentCheckName
    {
        get => _currentCheckName;
        private set
        {
            if (SetProperty(ref _currentCheckName, value))
            {
                OnPropertiesChanged(nameof(StatusTitle), nameof(StatusSummary));
            }
        }
    }

    public bool HasReport => Report is not null;

    public bool HasRecommendations => Report?.Diagnosis.RecommendedActions.Count > 0;

    public DiagnosticOutcome StatusOutcome => Report?.Diagnosis.Outcome ?? DiagnosticOutcome.Unknown;

    public string StatusTitle => IsRunning
        ? "Checking your connection"
        : Report?.Diagnosis.Headline ?? "Ready to diagnose your network";

    public string StatusSummary => IsRunning
        ? string.IsNullOrWhiteSpace(CurrentCheckName)
            ? "Preparing network checks…"
            : $"Running {CurrentCheckName.ToLowerInvariant()}…"
        : Report?.Diagnosis.Summary
          ?? "NetCheck will test the adapter, local network, DNS, internet access, and connection quality.";

    public string PrimaryAdapterName => Report?.Network.PrimaryAdapter?.Name ?? "Not available";

    public string PrimaryIpAddress => Report?.Network.PrimaryIpAddress ?? "Not available";

    public string PrimaryGateway => Report?.Network.PrimaryGateway ?? "Not available";

    public string PrimaryDnsServer => Report?.Network.PrimaryDnsServer ?? "Not available";

    public string CompletedText => Report is null
        ? string.Empty
        : $"Completed {Report.CompletedAtUtc.ToLocalTime():MMM d, yyyy 'at' HH:mm} in {Report.Duration.TotalSeconds:0.0} seconds";

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        _settings = await _settingsStore.LoadAsync().ConfigureAwait(true);
        if (_settings.AutoRunOnLaunch)
        {
            await RunAsync().ConfigureAwait(true);
        }
    }

    private async Task RunAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _runCancellation?.Dispose();
        _runCancellation = new CancellationTokenSource();
        var cancellationToken = _runCancellation.Token;
        IsRunning = true;
        ProgressPercentage = 0;
        CurrentCheckName = string.Empty;
        Report = null;
        Results.Clear();

        try
        {
            _settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(true);
            var progress = new Progress<DiagnosticProgress>(OnProgress);
            var report = await _diagnosticEngine
                .RunAsync(_settings, progress, cancellationToken)
                .ConfigureAwait(true);

            Report = report;
            ProgressPercentage = report.Diagnosis.Outcome == DiagnosticOutcome.Cancelled ? ProgressPercentage : 100;
            if (_settings.SaveDiagnosticHistory
                && report.Diagnosis.Outcome != DiagnosticOutcome.Cancelled)
            {
                try
                {
                    await _historyStore.SaveAsync(report, cancellationToken).ConfigureAwait(true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _logger.Error("Could not save diagnostic history.", exception);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Report = new DiagnosticReport
            {
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Network = new NetworkSnapshot(),
                Checks = Results.ToArray(),
                Diagnosis = new Diagnosis
                {
                    Outcome = DiagnosticOutcome.Cancelled,
                    Headline = "Diagnostic cancelled",
                    Summary = "The diagnostic was stopped before every check completed.",
                    RecommendedActions = ["Run a new diagnostic when you are ready."]
                }
            };
        }
        catch (Exception exception)
        {
            _logger.Error("Diagnostic run failed.", exception);
            _messageService.ShowError(
                "NetCheck could not finish",
                "An unexpected error interrupted the diagnostic. No system settings were changed. Please try again.");
        }
        finally
        {
            IsRunning = false;
            CurrentCheckName = string.Empty;
        }
    }

    private void OnProgress(DiagnosticProgress progress)
    {
        ProgressPercentage = progress.Percentage;
        CurrentCheckName = progress.CurrentCheckName;
        if (progress.Result is not null)
        {
            Results.Add(progress.Result);
        }
    }

    private void Cancel() => _runCancellation?.Cancel();

    private async Task ExportAsync()
    {
        if (Report is null)
        {
            return;
        }

        var suggestedName = $"NetCheck-{Report.CompletedAtUtc.ToLocalTime():yyyyMMdd-HHmm}.html";
        var path = _fileDialogService.ShowReportSaveDialog(suggestedName);
        if (path is null)
        {
            return;
        }

        try
        {
            await _reportExporter.ExportAsync(
                Report,
                path,
                _settings.IncludeComputerNameInExports).ConfigureAwait(true);
            _messageService.ShowInformation("Report exported", $"The diagnostic report was saved to:\n{path}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.Error("Report export failed.", exception);
            _messageService.ShowError("Export failed", exception.Message);
        }
    }

    private void CopySummary()
    {
        if (Report is null)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"NetCheck: {Report.Diagnosis.Headline}");
        builder.AppendLine(Report.Diagnosis.Summary);
        builder.AppendLine($"Adapter: {PrimaryAdapterName}");
        builder.AppendLine($"IP: {PrimaryIpAddress} | Gateway: {PrimaryGateway} | DNS: {PrimaryDnsServer}");
        foreach (var result in Report.Checks.Where(result => result.Status is CheckStatus.Warning or CheckStatus.Failed))
        {
            builder.AppendLine($"{result.Title}: {result.Summary}");
        }

        try
        {
            Clipboard.SetText(builder.ToString());
        }
        catch (Exception exception)
        {
            _logger.Error("Could not copy report summary.", exception);
            _messageService.ShowError("Copy failed", "Windows could not access the clipboard. Please try again.");
        }
    }

    private void AttachFailureHandler(AsyncRelayCommand command, string operation) =>
        command.ExecutionFailed += (_, exception) =>
        {
            _logger.Error($"Unexpected error while {operation}.", exception);
            _messageService.ShowError("Unexpected error", "NetCheck handled an unexpected error. Please try again.");
        };
}
