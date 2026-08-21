using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using NetCheck.App.Localization;
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
    private readonly INetworkRepairPlanner _repairPlanner;
    private readonly INetworkRepairService _repairService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IMessageService _messageService;
    private readonly LocalizationService _text;
    private readonly ReportLocalizationService _reportLocalization;
    private readonly FileLogger _logger;
    private CancellationTokenSource? _runCancellation;
    private DiagnosticReport? _report;
    private DiagnosticReport? _sourceReport;
    private DiagnosticOptions _settings = new();
    private NetworkRepairPlan _repairPlan = NetworkRepairPlan.Empty;
    private NetworkRepairResult? _repairResult;
    private NetworkRepairResult? _sourceRepairResult;
    private bool _isRunning;
    private bool _isRepairing;
    private bool _isInitialized;
    private bool _preserveRepairResultDuringVerification;
    private int _progressPercentage;
    private string _currentCheckName = string.Empty;

    public DashboardViewModel(
        IDiagnosticEngine diagnosticEngine,
        IReportHistoryStore historyStore,
        IReportExporter reportExporter,
        ISettingsStore settingsStore,
        INetworkRepairPlanner repairPlanner,
        INetworkRepairService repairService,
        IFileDialogService fileDialogService,
        IMessageService messageService,
        LocalizationService text,
        ReportLocalizationService reportLocalization,
        FileLogger logger)
    {
        _diagnosticEngine = diagnosticEngine ?? throw new ArgumentNullException(nameof(diagnosticEngine));
        _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
        _reportExporter = reportExporter ?? throw new ArgumentNullException(nameof(reportExporter));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _repairPlanner = repairPlanner ?? throw new ArgumentNullException(nameof(repairPlanner));
        _repairService = repairService ?? throw new ArgumentNullException(nameof(repairService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _reportLocalization = reportLocalization ?? throw new ArgumentNullException(nameof(reportLocalization));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsRunning && !IsRepairing);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => Report is not null && !IsRunning && !IsRepairing);
        CopySummaryCommand = new RelayCommand(CopySummary, () => Report is not null && !IsRepairing);
        FixCommand = new AsyncRelayCommand(FixAsync, () => CanFix);
        AttachFailureHandler(RunCommand, "running diagnostics");
        AttachFailureHandler(ExportCommand, "exporting a report");
        AttachFailureHandler(FixCommand, "repairing the network");
    }

    public ObservableCollection<DiagnosticCheckResult> Results { get; } = [];

    public AsyncRelayCommand RunCommand { get; }

    public RelayCommand CancelCommand { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public RelayCommand CopySummaryCommand { get; }

    public AsyncRelayCommand FixCommand { get; }

    public DiagnosticReport? Report
    {
        get => _report;
        private set
        {
            if (!SetProperty(ref _report, value))
            {
                return;
            }

            if (!_preserveRepairResultDuringVerification)
            {
                RepairResult = null;
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
                nameof(HasRecommendations),
                nameof(CanFix),
                nameof(ShowFixButton),
                nameof(FixButtonText),
                nameof(FixButtonToolTip));
            ExportCommand.RaiseCanExecuteChanged();
            CopySummaryCommand.RaiseCanExecuteChanged();
            FixCommand.RaiseCanExecuteChanged();
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

            OnPropertiesChanged(nameof(StatusTitle), nameof(StatusSummary), nameof(CanFix), nameof(FixButtonText));
            RunCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
            CopySummaryCommand.RaiseCanExecuteChanged();
            FixCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsRepairing
    {
        get => _isRepairing;
        private set
        {
            if (!SetProperty(ref _isRepairing, value))
            {
                return;
            }

            OnPropertiesChanged(
                nameof(StatusTitle),
                nameof(StatusSummary),
                nameof(CanFix),
                nameof(FixButtonText));
            RunCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
            CopySummaryCommand.RaiseCanExecuteChanged();
            FixCommand.RaiseCanExecuteChanged();
        }
    }

    public NetworkRepairPlan RepairPlan
    {
        get => _repairPlan;
        private set
        {
            if (SetProperty(ref _repairPlan, value))
            {
                OnPropertiesChanged(nameof(CanFix), nameof(FixButtonText), nameof(FixButtonToolTip));
                FixCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public NetworkRepairResult? RepairResult
    {
        get => _repairResult;
        private set
        {
            if (SetProperty(ref _repairResult, value))
            {
                OnPropertiesChanged(
                    nameof(HasRepairResult),
                    nameof(RepairResultTitle),
                    nameof(RepairResultSummary));
            }
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

    public bool CanFix => Report is not null && RepairPlan.CanExecute && !IsRunning && !IsRepairing;

    public bool ShowFixButton => Report?.Diagnosis.Outcome is DiagnosticOutcome.Attention or DiagnosticOutcome.Problem;

    public bool HasRepairResult => RepairResult is not null;

    public string FixButtonText => IsRepairing
        ? _text.Translate("Fixing issues…")
        : !RepairPlan.CanExecute
            ? _text.Translate("Fix unavailable")
        : RepairPlan.Actions.Count == 1
            ? _text.Translate("Fix issue")
            : _text.Format("Fix {0} issues", RepairPlan.Actions.Count);

    public string FixButtonToolTip => RepairPlan.CanExecute
        ? _text.Translate("Review and apply the repair plan for the detected issues.")
        : _text.Translate("This issue needs a manual, physical, router, or provider fix; no safe Windows repair matches the evidence.");

    public string RepairResultTitle => RepairResult switch
    {
        null => string.Empty,
        { Cancelled: true } => _text.Translate("Repair cancelled"),
        { Succeeded: true } => _text.Translate("Approved repairs were applied"),
        { HasAppliedChanges: true } => _text.Translate("Some repairs were applied"),
        _ => _text.Translate("Repairs could not be applied")
    };

    public string RepairResultSummary => RepairResult switch
    {
        null => string.Empty,
        { Cancelled: true } => _text.Translate("No changes were made because administrator approval was cancelled."),
        { Succeeded: true, RequiresRestart: true } =>
            _text.Translate("Restart Windows to finish the network-stack repair, then run NetCheck again."),
        { Succeeded: true } =>
            _text.Translate("NetCheck applied the repair plan and checked the connection again."),
        { HasAppliedChanges: true, RequiresRestart: true } =>
            _text.Translate("Windows applied part of the plan. Restart the computer before checking again."),
        { HasAppliedChanges: true } =>
            _text.Translate("Windows applied part of the plan and NetCheck checked the connection again."),
        _ => _text.Translate("Review the failed steps below and use the recommended manual next steps.")
    };

    public DiagnosticOutcome StatusOutcome => Report?.Diagnosis.Outcome ?? DiagnosticOutcome.Unknown;

    public string StatusTitle => IsRepairing
        ? _text.Translate("Applying approved repairs")
        : IsRunning
            ? _text.Translate("Checking your connection")
            : Report?.Diagnosis.Headline ?? _text.Translate("Ready to diagnose your network");

    public string StatusSummary => IsRepairing
        ? _text.Translate("Windows may ask for administrator approval. NetCheck will only run the repairs shown in the confirmation.")
        : IsRunning
            ? string.IsNullOrWhiteSpace(CurrentCheckName)
                ? _text.Translate("Preparing network checks…")
                : _text.Format("Running {0}…", CurrentCheckName.ToLower(_text.Culture))
            : Report?.Diagnosis.Summary
              ?? _text.Translate("NetCheck will test the adapter, local network, DNS, internet access, and connection quality.");

    public string PrimaryAdapterName => Report?.Network.PrimaryAdapter?.Name ?? _text.Translate("Not available");

    public string PrimaryIpAddress => Report is null
        ? _text.Translate("Not available")
        : _text.Translate(Report.Network.PrimaryIpAddress);

    public string PrimaryGateway => Report is null
        ? _text.Translate("Not available")
        : _text.Translate(Report.Network.PrimaryGateway);

    public string PrimaryDnsServer => Report is null
        ? _text.Translate("Not available")
        : _text.Translate(Report.Network.PrimaryDnsServer);

    public string CompletedText => Report is null
        ? string.Empty
        : _text.Format(
            "Completed {0} in {1:0.0} seconds",
            Report.CompletedAtUtc.ToLocalTime().ToString("g", _text.Culture),
            Report.Duration.TotalSeconds);

    public void RefreshLocalization()
    {
        var previousPreserveState = _preserveRepairResultDuringVerification;
        _preserveRepairResultDuringVerification = true;
        try
        {
            SetReport(_sourceReport);
        }
        finally
        {
            _preserveRepairResultDuringVerification = previousPreserveState;
        }

        RepairResult = _sourceRepairResult is null
            ? null
            : _reportLocalization.Localize(_sourceRepairResult);
        Results.Clear();
        if (_sourceReport is not null)
        {
            foreach (var result in _sourceReport.Checks)
            {
                Results.Add(_reportLocalization.Localize(result));
            }
        }

        OnPropertiesChanged(
            nameof(StatusTitle),
            nameof(StatusSummary),
            nameof(FixButtonText),
            nameof(FixButtonToolTip),
            nameof(RepairResultTitle),
            nameof(RepairResultSummary),
            nameof(PrimaryAdapterName),
            nameof(PrimaryIpAddress),
            nameof(PrimaryGateway),
            nameof(PrimaryDnsServer),
            nameof(CompletedText));
    }

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
        if (IsRunning || IsRepairing)
        {
            return;
        }

        _runCancellation?.Dispose();
        _runCancellation = new CancellationTokenSource();
        var cancellationToken = _runCancellation.Token;
        IsRunning = true;
        ProgressPercentage = 0;
        CurrentCheckName = string.Empty;
        SetReport(null);
        Results.Clear();

        try
        {
            _settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(true);
            var progress = new Progress<DiagnosticProgress>(OnProgress);
            var report = await _diagnosticEngine
                .RunAsync(_settings, progress, cancellationToken)
                .ConfigureAwait(true);

            SetReport(report);
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
            SetReport(new DiagnosticReport
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
            });
        }
        catch (Exception exception)
        {
            _logger.Error("Diagnostic run failed.", exception);
            _messageService.ShowError(
                _text.Translate("NetCheck could not finish"),
                _text.Translate("An unexpected error interrupted the diagnostic. No system settings were changed. Please try again."));
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
            Results.Add(_reportLocalization.Localize(progress.Result));
        }
    }

    private void Cancel() => _runCancellation?.Cancel();

    private async Task FixAsync()
    {
        var plan = RepairPlan;
        if (!CanFix || !plan.CanExecute)
        {
            return;
        }

        var message = new StringBuilder();
        message.AppendLine(_text.Translate("NetCheck can try these repairs:"));
        message.AppendLine();
        foreach (var action in plan.Actions)
        {
            message.AppendLine($"• {action.Title}");
            message.AppendLine($"  {action.Description}");
        }

        if (plan.RequiresElevation)
        {
            message.AppendLine();
            message.AppendLine(_text.Translate("Windows will ask for administrator approval."));
        }

        if (plan.RequiresRestart)
        {
            message.AppendLine(_text.Translate("One or more repairs require a Windows restart."));
        }

        message.AppendLine();
        message.Append(_text.Translate("Only the listed changes will be made. Continue?"));
        if (!_messageService.Confirm(_text.Translate("Fix detected network issues?"), message.ToString()))
        {
            return;
        }

        IsRepairing = true;
        NetworkRepairResult result;
        try
        {
            result = await _repairService.ExecuteAsync(plan).ConfigureAwait(true);
            SetRepairResult(result);
        }
        finally
        {
            IsRepairing = false;
        }

        if (!result.Cancelled && result.HasAppliedChanges && !result.RequiresRestart)
        {
            _preserveRepairResultDuringVerification = true;
            try
            {
                await RunAsync().ConfigureAwait(true);
            }
            finally
            {
                _preserveRepairResultDuringVerification = false;
            }
        }
    }

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
            _messageService.ShowInformation(
                _text.Translate("Report exported"),
                _text.Format("The diagnostic report was saved to:\n{0}", path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.Error("Report export failed.", exception);
            _messageService.ShowError(_text.Translate("Export failed"), exception.Message);
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
        builder.AppendLine($"{_text.Translate("Adapter")}: {PrimaryAdapterName}");
        builder.AppendLine($"IP: {PrimaryIpAddress} | {_text.Translate("Gateway")}: {PrimaryGateway} | DNS: {PrimaryDnsServer}");
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
            _messageService.ShowError(
                _text.Translate("Copy failed"),
                _text.Translate("Windows could not access the clipboard. Please try again."));
        }
    }

    private void AttachFailureHandler(AsyncRelayCommand command, string operation) =>
        command.ExecutionFailed += (_, exception) =>
        {
            _logger.Error($"Unexpected error while {operation}.", exception);
            _messageService.ShowError(
                _text.Translate("Unexpected error"),
                _text.Translate("NetCheck handled an unexpected error. Please try again."));
        };

    private void SetReport(DiagnosticReport? source)
    {
        _sourceReport = source;
        if (!_preserveRepairResultDuringVerification)
        {
            _sourceRepairResult = null;
        }

        RepairPlan = source is null
            ? NetworkRepairPlan.Empty
            : _reportLocalization.Localize(_repairPlanner.CreatePlan(source));
        Report = source is null ? null : _reportLocalization.Localize(source);
    }

    private void SetRepairResult(NetworkRepairResult result)
    {
        _sourceRepairResult = result;
        RepairResult = _reportLocalization.Localize(result);
    }
}
