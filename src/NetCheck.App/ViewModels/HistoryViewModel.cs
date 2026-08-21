using System.Collections.ObjectModel;
using System.IO;
using NetCheck.App.Mvvm;
using NetCheck.App.Services;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Logging;

namespace NetCheck.App.ViewModels;

public sealed class HistoryViewModel : ObservableObject
{
    private readonly IReportHistoryStore _historyStore;
    private readonly IReportExporter _reportExporter;
    private readonly ISettingsStore _settingsStore;
    private readonly IFileDialogService _fileDialogService;
    private readonly IMessageService _messageService;
    private readonly FileLogger _logger;
    private DiagnosticReport? _selectedReport;
    private bool _isBusy;

    public HistoryViewModel(
        IReportHistoryStore historyStore,
        IReportExporter reportExporter,
        ISettingsStore settingsStore,
        IFileDialogService fileDialogService,
        IMessageService messageService,
        FileLogger logger)
    {
        _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
        _reportExporter = reportExporter ?? throw new ArgumentNullException(nameof(reportExporter));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => SelectedReport is not null && !IsBusy);
        ClearCommand = new AsyncRelayCommand(ClearAsync, () => Reports.Count > 0 && !IsBusy);
        RefreshCommand.ExecutionFailed += OnCommandFailed;
        ExportCommand.ExecutionFailed += OnCommandFailed;
        ClearCommand.ExecutionFailed += OnCommandFailed;
    }

    public ObservableCollection<DiagnosticReport> Reports { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public AsyncRelayCommand ClearCommand { get; }

    public DiagnosticReport? SelectedReport
    {
        get => _selectedReport;
        set
        {
            if (SetProperty(ref _selectedReport, value))
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

    public bool HasReports => Reports.Count > 0;

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var selectedId = SelectedReport?.Id;
            var reports = await _historyStore.GetRecentAsync(50).ConfigureAwait(true);
            Reports.Clear();
            foreach (var report in reports)
            {
                Reports.Add(report);
            }

            SelectedReport = Reports.FirstOrDefault(report => report.Id == selectedId) ?? Reports.FirstOrDefault();
            OnPropertyChanged(nameof(HasReports));
            ClearCommand.RaiseCanExecuteChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Error("Could not load report history.", exception);
            _messageService.ShowError("History unavailable", "NetCheck could not load the saved diagnostic history.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportAsync()
    {
        if (SelectedReport is null)
        {
            return;
        }

        var path = _fileDialogService.ShowReportSaveDialog(
            $"NetCheck-{SelectedReport.CompletedAtUtc.ToLocalTime():yyyyMMdd-HHmm}.html");
        if (path is null)
        {
            return;
        }

        var settings = await _settingsStore.LoadAsync().ConfigureAwait(true);
        await _reportExporter.ExportAsync(
            SelectedReport,
            path,
            settings.IncludeComputerNameInExports).ConfigureAwait(true);
        _messageService.ShowInformation("Report exported", $"The diagnostic report was saved to:\n{path}");
    }

    private async Task ClearAsync()
    {
        if (!_messageService.Confirm(
                "Clear diagnostic history?",
                "This permanently removes the diagnostic reports saved by NetCheck on this computer."))
        {
            return;
        }

        await _historyStore.ClearAsync().ConfigureAwait(true);
        Reports.Clear();
        SelectedReport = null;
        OnPropertyChanged(nameof(HasReports));
        ClearCommand.RaiseCanExecuteChanged();
    }

    private void OnCommandFailed(object? sender, Exception exception)
    {
        _logger.Error("History operation failed.", exception);
        _messageService.ShowError("Operation failed", exception.Message);
    }
}
