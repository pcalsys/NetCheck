using System.Collections.ObjectModel;
using System.IO;
using NetCheck.App.Localization;
using NetCheck.App.Mvvm;
using NetCheck.App.Services;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Logging;

namespace NetCheck.App.ViewModels;

public sealed class HistoryViewModel : ObservableObject
{
    private const int MaximumHistoryItems = 100;
    private readonly IReportHistoryStore _reportHistoryStore;
    private readonly IActivityHistoryStore _activityHistoryStore;
    private readonly IMonitoringHistoryStore _monitoringHistoryStore;
    private readonly IReportExporter _reportExporter;
    private readonly ISettingsStore _settingsStore;
    private readonly IFileDialogService _fileDialogService;
    private readonly IMessageService _messageService;
    private readonly LocalizationService _text;
    private readonly ReportLocalizationService _reportLocalization;
    private readonly FileLogger _logger;
    private IReadOnlyList<DiagnosticReport> _sourceReports = Array.Empty<DiagnosticReport>();
    private IReadOnlyList<ActivityHistoryEntry> _sourceActivities = Array.Empty<ActivityHistoryEntry>();
    private IReadOnlyList<MonitoringSession> _sourceMonitoringSessions = Array.Empty<MonitoringSession>();
    private HistoryItemViewModel? _selectedItem;
    private bool _isBusy;

    public HistoryViewModel(
        IReportHistoryStore reportHistoryStore,
        IActivityHistoryStore activityHistoryStore,
        IMonitoringHistoryStore monitoringHistoryStore,
        IReportExporter reportExporter,
        ISettingsStore settingsStore,
        IFileDialogService fileDialogService,
        IMessageService messageService,
        LocalizationService text,
        ReportLocalizationService reportLocalization,
        FileLogger logger)
    {
        _reportHistoryStore = reportHistoryStore ?? throw new ArgumentNullException(nameof(reportHistoryStore));
        _activityHistoryStore = activityHistoryStore ?? throw new ArgumentNullException(nameof(activityHistoryStore));
        _monitoringHistoryStore = monitoringHistoryStore ?? throw new ArgumentNullException(nameof(monitoringHistoryStore));
        _reportExporter = reportExporter ?? throw new ArgumentNullException(nameof(reportExporter));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _reportLocalization = reportLocalization ?? throw new ArgumentNullException(nameof(reportLocalization));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => SelectedItem?.CanExport == true && !IsBusy);
        ClearCommand = new AsyncRelayCommand(ClearAsync, () => Items.Count > 0 && !IsBusy);
        RefreshCommand.ExecutionFailed += OnCommandFailed;
        ExportCommand.ExecutionFailed += OnCommandFailed;
        ClearCommand.ExecutionFailed += OnCommandFailed;
    }

    public ObservableCollection<HistoryItemViewModel> Items { get; } = [];

    public ObservableCollection<double?> SpeedDownloadTrend { get; } = [];

    public ObservableCollection<double?> SpeedUploadTrend { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public AsyncRelayCommand ClearCommand { get; }

    public HistoryItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                ExportCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            RefreshCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
            ClearCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasItems => Items.Count > 0;

    public bool HasSpeedHistory => SpeedDownloadTrend.Count >= 2;

    public void RefreshLocalization()
    {
        var selectedId = SelectedItem?.Id;
        RebuildItems();
        SelectedItem = Items.FirstOrDefault(item => item.Id == selectedId) ?? Items.FirstOrDefault();
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var selectedId = SelectedItem?.Id;
            var reportsTask = _reportHistoryStore.GetRecentAsync(MaximumHistoryItems);
            var activitiesTask = _activityHistoryStore.GetRecentAsync(MaximumHistoryItems);
            var monitoringTask = _monitoringHistoryStore.GetRecentAsync(MaximumHistoryItems);
            await Task.WhenAll(reportsTask, activitiesTask, monitoringTask).ConfigureAwait(true);
            _sourceReports = await reportsTask.ConfigureAwait(true);
            _sourceActivities = await activitiesTask.ConfigureAwait(true);
            _sourceMonitoringSessions = await monitoringTask.ConfigureAwait(true);
            RebuildItems();
            SelectedItem = Items.FirstOrDefault(item => item.Id == selectedId) ?? Items.FirstOrDefault();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Could not load local history.", exception);
            _messageService.ShowError(
                _text.Translate("History unavailable"),
                _text.Translate("NetCheck could not load the saved local history."));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildItems()
    {
        var reports = _sourceReports
            .Select(_reportLocalization.Localize)
            .Select(report => HistoryItemViewModel.FromReport(report, _text));
        var activities = _sourceActivities
            .Select(activity => HistoryItemViewModel.FromActivity(activity, _text));
        var monitoringSessions = _sourceMonitoringSessions
            .Select(session => HistoryItemViewModel.FromMonitoringSession(session, _text));

        Items.Clear();
        foreach (var item in reports
                     .Concat(activities)
                     .Concat(monitoringSessions)
                     .OrderByDescending(item => item.OccurredAtUtc)
                     .Take(MaximumHistoryItems))
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(HasItems));
        RebuildSpeedTrend();
        ClearCommand.RaiseCanExecuteChanged();
    }

    private async Task ExportAsync()
    {
        var report = SelectedItem?.Report;
        if (report is null)
        {
            return;
        }

        var path = _fileDialogService.ShowReportSaveDialog(
            $"NetCheck-{report.CompletedAtUtc.ToLocalTime():yyyyMMdd-HHmm}.html");
        if (path is null)
        {
            return;
        }

        var settings = await _settingsStore.LoadAsync().ConfigureAwait(true);
        await _reportExporter.ExportAsync(
            report,
            path,
            settings.IncludeComputerNameInExports).ConfigureAwait(true);
        _messageService.ShowInformation(
            _text.Translate("Report exported"),
            _text.Format("The diagnostic report was saved to:\n{0}", path));
    }

    private async Task ClearAsync()
    {
        if (!_messageService.Confirm(
                _text.Translate("Clear local history?"),
                _text.Translate("This permanently removes diagnostics, speed tests, monitoring sessions, and configuration changes saved by NetCheck on this computer.")))
        {
            return;
        }

        await _reportHistoryStore.ClearAsync().ConfigureAwait(true);
        await _activityHistoryStore.ClearAsync().ConfigureAwait(true);
        await _monitoringHistoryStore.ClearAsync().ConfigureAwait(true);
        Items.Clear();
        _sourceReports = Array.Empty<DiagnosticReport>();
        _sourceActivities = Array.Empty<ActivityHistoryEntry>();
        _sourceMonitoringSessions = Array.Empty<MonitoringSession>();
        SpeedDownloadTrend.Clear();
        SpeedUploadTrend.Clear();
        SelectedItem = null;
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasSpeedHistory));
        ClearCommand.RaiseCanExecuteChanged();
    }

    private void RebuildSpeedTrend()
    {
        var speedTests = _sourceActivities
            .Where(activity => activity.Kind == ActivityHistoryKind.SpeedTest)
            .Select(activity => activity.SpeedTestResult)
            .OfType<SpeedTestResult>()
            .OrderBy(result => result.CompletedAtUtc)
            .TakeLast(30)
            .ToArray();
        SpeedDownloadTrend.Clear();
        SpeedUploadTrend.Clear();
        foreach (var result in speedTests)
        {
            SpeedDownloadTrend.Add(result.DownloadMegabitsPerSecond);
            SpeedUploadTrend.Add(result.UploadMegabitsPerSecond);
        }

        OnPropertyChanged(nameof(HasSpeedHistory));
    }

    private void OnCommandFailed(object? sender, Exception exception)
    {
        _logger.Error("History operation failed.", exception);
        _messageService.ShowError(_text.Translate("Operation failed"), exception.Message);
    }
}
