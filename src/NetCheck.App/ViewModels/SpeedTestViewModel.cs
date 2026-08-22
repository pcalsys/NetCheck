using NetCheck.App.Localization;
using NetCheck.App.Mvvm;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Logging;

namespace NetCheck.App.ViewModels;

public sealed class SpeedTestViewModel : ObservableObject
{
    private readonly ISpeedTestService _speedTestService;
    private readonly IActivityHistoryStore _activityHistoryStore;
    private readonly LocalizationService _text;
    private readonly FileLogger _logger;
    private CancellationTokenSource? _runCancellation;
    private SpeedTestResult? _result;
    private bool _isRunning;
    private int _progressPercentage;
    private double _currentMegabitsPerSecond;
    private string _statusSource = "Ready to measure your connection";
    private string _errorSource = string.Empty;

    public SpeedTestViewModel(
        ISpeedTestService speedTestService,
        IActivityHistoryStore activityHistoryStore,
        LocalizationService text,
        FileLogger logger)
    {
        _speedTestService = speedTestService ?? throw new ArgumentNullException(nameof(speedTestService));
        _activityHistoryStore = activityHistoryStore ?? throw new ArgumentNullException(nameof(activityHistoryStore));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsRunning);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        RunCommand.ExecutionFailed += (_, exception) =>
            _logger.Error("Unhandled speed-test operation failure.", exception);
    }

    public AsyncRelayCommand RunCommand { get; }

    public RelayCommand CancelCommand { get; }

    public SpeedTestResult? Result
    {
        get => _result;
        private set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertiesChanged(
                    nameof(HasResult),
                    nameof(PeakDownloadText),
                    nameof(AverageDownloadText),
                    nameof(PeakUploadText),
                    nameof(AverageUploadText),
                    nameof(LatencyText),
                    nameof(DurationText),
                    nameof(DataUsedText));
            }
        }
    }

    public bool HasResult => Result is not null;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertiesChanged(nameof(CurrentSpeedText));
                RunCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int ProgressPercentage
    {
        get => _progressPercentage;
        private set => SetProperty(ref _progressPercentage, Math.Clamp(value, 0, 100));
    }

    public double CurrentMegabitsPerSecond
    {
        get => _currentMegabitsPerSecond;
        private set
        {
            if (SetProperty(ref _currentMegabitsPerSecond, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(CurrentSpeedText));
            }
        }
    }

    public string StatusText => _text.Translate(_statusSource);

    public string ErrorMessage => _text.Translate(_errorSource);

    public string CurrentSpeedText => IsRunning && CurrentMegabitsPerSecond > 0
        ? FormatSpeed(CurrentMegabitsPerSecond)
        : "—";

    public string PeakDownloadText => FormatSpeed(Result?.PeakDownloadMegabitsPerSecond);

    public string AverageDownloadText => FormatSpeed(Result?.DownloadMegabitsPerSecond);

    public string PeakUploadText => FormatSpeed(Result?.PeakUploadMegabitsPerSecond);

    public string AverageUploadText => FormatSpeed(Result?.UploadMegabitsPerSecond);

    public string LatencyText => Result is null
        ? "—"
        : string.Format(_text.Culture, "{0:N0} ms", Result.LatencyMilliseconds);

    public string DurationText => Result is null
        ? "—"
        : string.Format(_text.Culture, "{0:N1} s", Result.Duration.TotalSeconds);

    public string DataUsedText => Result is null
        ? "—"
        : string.Format(
            _text.Culture,
            "{0:N1} MB",
            (Result.DownloadBytes + Result.UploadBytes) / 1_000_000d);

    public void RefreshLocalization()
    {
        OnPropertiesChanged(
            nameof(StatusText),
            nameof(ErrorMessage),
            nameof(CurrentSpeedText),
            nameof(PeakDownloadText),
            nameof(AverageDownloadText),
            nameof(PeakUploadText),
            nameof(AverageUploadText),
            nameof(LatencyText),
            nameof(DurationText),
            nameof(DataUsedText));
    }

    private async Task RunAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _runCancellation?.Dispose();
        var runCancellation = new CancellationTokenSource();
        _runCancellation = runCancellation;
        Result = null;
        ProgressPercentage = 0;
        CurrentMegabitsPerSecond = 0;
        SetError(string.Empty);
        SetStatus("Preparing the speed test…");
        IsRunning = true;

        var progress = new Progress<SpeedTestProgress>(ApplyProgress);
        try
        {
            Result = await _speedTestService
                .RunAsync(progress, runCancellation.Token)
                .ConfigureAwait(true);
            await SaveResultToHistoryAsync(Result).ConfigureAwait(true);
            ProgressPercentage = 100;
            CurrentMegabitsPerSecond = 0;
            SetStatus("Speed test complete");
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
            CurrentMegabitsPerSecond = 0;
            SetStatus("Speed test cancelled. No result was saved.");
        }
        catch (SpeedTestException exception)
        {
            _logger.Error("Speed test failed.", exception);
            CurrentMegabitsPerSecond = 0;
            SetStatus("NetCheck could not finish the speed test. Try again.");
            SetError(exception.Failure switch
            {
                SpeedTestFailure.TimedOut =>
                    "The speed test timed out. The connection may be too slow or unstable.",
                SpeedTestFailure.UnexpectedResponse =>
                    "The speed-test service returned an unexpected response. A sign-in page or network filter may be interfering.",
                _ =>
                    "The speed-test service could not be reached. Check the connection and try again."
            });
        }
        catch (Exception exception)
        {
            _logger.Error("Unexpected speed-test failure.", exception);
            CurrentMegabitsPerSecond = 0;
            SetStatus("NetCheck could not finish the speed test. Try again.");
            SetError("An unexpected error interrupted the speed test. No result was saved.");
        }
        finally
        {
            IsRunning = false;
            runCancellation.Dispose();
            if (ReferenceEquals(_runCancellation, runCancellation))
            {
                _runCancellation = null;
            }
        }
    }

    private void Cancel() => _runCancellation?.Cancel();

    private async Task SaveResultToHistoryAsync(SpeedTestResult result)
    {
        try
        {
            await _activityHistoryStore.SaveAsync(new ActivityHistoryEntry
            {
                OccurredAtUtc = result.CompletedAtUtc,
                Kind = ActivityHistoryKind.SpeedTest,
                SpeedTestResult = result
            }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            // A local history failure must not discard a successfully measured result.
            _logger.Error("Could not save the speed-test result to local history.", exception);
        }
    }

    private void ApplyProgress(SpeedTestProgress progress)
    {
        ProgressPercentage = progress.Percentage;
        CurrentMegabitsPerSecond = progress.CurrentMegabitsPerSecond;
        SetStatus(progress.Phase switch
        {
            SpeedTestPhase.Preparing => "Preparing the speed test…",
            SpeedTestPhase.Latency => "Measuring latency…",
            SpeedTestPhase.Download => "Measuring download speed…",
            SpeedTestPhase.Upload => "Measuring upload speed…",
            SpeedTestPhase.Complete => "Speed test complete",
            _ => "Preparing the speed test…"
        });
    }

    private void SetStatus(string source)
    {
        _statusSource = source;
        OnPropertyChanged(nameof(StatusText));
    }

    private void SetError(string source)
    {
        _errorSource = source;
        OnPropertyChanged(nameof(ErrorMessage));
    }

    private string FormatSpeed(double? value) => value is null
        ? "—"
        : string.Format(_text.Culture, "{0:N1} Mbit/s", value.Value);
}
