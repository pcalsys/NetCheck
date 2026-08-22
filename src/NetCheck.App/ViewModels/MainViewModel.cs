using NetCheck.App.Mvvm;
using NetCheck.App.Localization;
using NetCheck.App.Services;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Logging;
using System.Reflection;

namespace NetCheck.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboard;
    private readonly SpeedTestViewModel _speedTest;
    private readonly MonitoringViewModel _monitoring;
    private readonly HistoryViewModel _history;
    private readonly SettingsViewModel _settings;
    private readonly ISettingsStore _settingsStore;
    private readonly IActivityHistoryStore _activityHistoryStore;
    private readonly LocalizationService _text;
    private readonly FileLogger _logger;
    private readonly INotificationService _notificationService;
    private ObservableObject _currentPage;
    private MainPage _selectedPage = MainPage.Dashboard;
    private string _menuLanguage = "en";
    private bool _isNotificationVisible;
    private string _notificationTitle = string.Empty;
    private string _notificationMessage = string.Empty;
    private NotificationKind _notificationKind;
    private CancellationTokenSource? _notificationCancellation;

    public MainViewModel(
        DashboardViewModel dashboard,
        SpeedTestViewModel speedTest,
        MonitoringViewModel monitoring,
        HistoryViewModel history,
        SettingsViewModel settings,
        ISettingsStore settingsStore,
        IActivityHistoryStore activityHistoryStore,
        INotificationService notificationService,
        LocalizationService text,
        FileLogger logger)
    {
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        _speedTest = speedTest ?? throw new ArgumentNullException(nameof(speedTest));
        _monitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _activityHistoryStore = activityHistoryStore ?? throw new ArgumentNullException(nameof(activityHistoryStore));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentPage = dashboard;

        ShowDashboardCommand = new RelayCommand(ShowDashboard);
        ShowSpeedTestCommand = new RelayCommand(ShowSpeedTest);
        ShowMonitoringCommand = new RelayCommand(ShowMonitoring);
        ShowHistoryCommand = new AsyncRelayCommand(ShowHistoryAsync);
        ShowSettingsCommand = new AsyncRelayCommand(ShowSettingsAsync);
        UseEnglishMenuCommand = new AsyncRelayCommand(() => SetMenuLanguageAsync("en"));
        UseGermanMenuCommand = new AsyncRelayCommand(() => SetMenuLanguageAsync("de"));
        DismissNotificationCommand = new RelayCommand(DismissNotification);
        _notificationService.NotificationRaised += OnNotificationRaised;
    }

    public RelayCommand ShowDashboardCommand { get; }

    public RelayCommand ShowSpeedTestCommand { get; }

    public RelayCommand ShowMonitoringCommand { get; }

    public AsyncRelayCommand ShowHistoryCommand { get; }

    public AsyncRelayCommand ShowSettingsCommand { get; }

    public AsyncRelayCommand UseEnglishMenuCommand { get; }

    public AsyncRelayCommand UseGermanMenuCommand { get; }

    public RelayCommand DismissNotificationCommand { get; }

    public ObservableObject CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string CurrentPageName => _selectedPage switch
    {
        MainPage.History => _text.Translate("History"),
        MainPage.Settings => _text.Translate("Settings"),
        MainPage.SpeedTest => _text.Translate("Speed test"),
        MainPage.Monitoring => _text.Translate("Monitoring"),
        _ => _text.Translate("Dashboard")
    };

    public string DashboardMenuLabel => _text.Translate("Dashboard");

    public string SpeedTestMenuLabel => _text.Translate("Speed test");

    public string MonitoringMenuLabel => _text.Translate("Monitoring");

    public string HistoryMenuLabel => _text.Translate("History");

    public string SettingsMenuLabel => _text.Translate("Settings");

    public string MenuLanguageLabel => _text.Translate("LANGUAGE");

    public string NavigationLabel => _text.Translate("NAVIGATION");

    public string CurrentLanguageTag => _text.Culture.IetfLanguageTag;

    public bool IsDashboardSelected => _selectedPage == MainPage.Dashboard;

    public bool IsSpeedTestSelected => _selectedPage == MainPage.SpeedTest;

    public bool IsMonitoringSelected => _selectedPage == MainPage.Monitoring;

    public bool IsHistorySelected => _selectedPage == MainPage.History;

    public bool IsSettingsSelected => _selectedPage == MainPage.Settings;

    public bool IsEnglishMenu => !IsGermanMenu;

    public bool IsGermanMenu => string.Equals(_menuLanguage, "de", StringComparison.Ordinal);

    public string VersionLabel => $"VERSION {GetApplicationVersion()}";

    public bool IsNotificationVisible
    {
        get => _isNotificationVisible;
        private set => SetProperty(ref _isNotificationVisible, value);
    }

    public string NotificationTitle
    {
        get => _notificationTitle;
        private set => SetProperty(ref _notificationTitle, value);
    }

    public string NotificationMessage
    {
        get => _notificationMessage;
        private set => SetProperty(ref _notificationMessage, value);
    }

    public NotificationKind NotificationKind
    {
        get => _notificationKind;
        private set => SetProperty(ref _notificationKind, value);
    }

    public async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync().ConfigureAwait(true);
        ApplyMenuLanguage(settings.MenuLanguage);
        await _dashboard.InitializeAsync().ConfigureAwait(true);
    }

    private void ShowDashboard()
    {
        CurrentPage = _dashboard;
        SelectPage(MainPage.Dashboard);
    }

    private void ShowSpeedTest()
    {
        CurrentPage = _speedTest;
        SelectPage(MainPage.SpeedTest);
    }

    private void ShowMonitoring()
    {
        CurrentPage = _monitoring;
        SelectPage(MainPage.Monitoring);
    }

    private async Task ShowHistoryAsync()
    {
        CurrentPage = _history;
        SelectPage(MainPage.History);
        await _history.LoadAsync().ConfigureAwait(true);
    }

    private async Task ShowSettingsAsync()
    {
        CurrentPage = _settings;
        SelectPage(MainPage.Settings);
        await _settings.LoadAsync().ConfigureAwait(true);
    }

    private async Task SetMenuLanguageAsync(string language)
    {
        var normalizedLanguage = string.Equals(language, "de", StringComparison.OrdinalIgnoreCase)
            ? "de"
            : "en";
        if (string.Equals(_menuLanguage, normalizedLanguage, StringComparison.Ordinal))
        {
            return;
        }

        var previousLanguage = _menuLanguage;
        ApplyMenuLanguage(normalizedLanguage);
        try
        {
            var settings = await _settingsStore.LoadAsync().ConfigureAwait(true);
            await _settingsStore.SaveAsync(settings with { MenuLanguage = normalizedLanguage }).ConfigureAwait(true);
            await SaveLanguageChangeToHistoryAsync(previousLanguage, normalizedLanguage).ConfigureAwait(true);
            if (_selectedPage == MainPage.History)
            {
                await _history.LoadAsync().ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            _logger.Error("Could not save the menu language preference.", exception);
        }
    }

    private async Task SaveLanguageChangeToHistoryAsync(string previousLanguage, string newLanguage)
    {
        try
        {
            await _activityHistoryStore.SaveAsync(new ActivityHistoryEntry
            {
                Kind = ActivityHistoryKind.LanguageChanged,
                SettingChanges =
                [
                    new SettingChange(
                        nameof(DiagnosticOptions.MenuLanguage),
                        previousLanguage,
                        newLanguage)
                ]
            }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _logger.Error("Could not save the language change to local history.", exception);
        }
    }

    private void ApplyMenuLanguage(string language)
    {
        _menuLanguage = string.Equals(language, "de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
        _text.SetLanguage(_menuLanguage);
        _dashboard.RefreshLocalization();
        _speedTest.RefreshLocalization();
        _monitoring.RefreshLocalization();
        _history.RefreshLocalization();
        _settings.RefreshLocalization();
        OnPropertiesChanged(
            nameof(CurrentPageName),
            nameof(DashboardMenuLabel),
            nameof(SpeedTestMenuLabel),
            nameof(MonitoringMenuLabel),
            nameof(HistoryMenuLabel),
            nameof(SettingsMenuLabel),
            nameof(MenuLanguageLabel),
            nameof(NavigationLabel),
            nameof(CurrentLanguageTag),
            nameof(IsEnglishMenu),
            nameof(IsGermanMenu));
    }

    private void SelectPage(MainPage page)
    {
        _selectedPage = page;
        OnPropertiesChanged(
            nameof(CurrentPageName),
            nameof(IsDashboardSelected),
            nameof(IsSpeedTestSelected),
            nameof(IsMonitoringSelected),
            nameof(IsHistorySelected),
            nameof(IsSettingsSelected));
    }

    private enum MainPage
    {
        Dashboard,
        SpeedTest,
        Monitoring,
        History,
        Settings
    }

    public Task ShutdownAsync() => _monitoring.ShutdownAsync();

    private async void OnNotificationRaised(object? sender, AppNotification notification)
    {
        _notificationCancellation?.Cancel();
        _notificationCancellation?.Dispose();
        _notificationCancellation = new CancellationTokenSource();
        NotificationTitle = notification.Title;
        NotificationMessage = notification.Message;
        NotificationKind = notification.Kind;
        IsNotificationVisible = true;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), _notificationCancellation.Token).ConfigureAwait(true);
            IsNotificationVisible = false;
        }
        catch (OperationCanceledException)
        {
            // A new notification or a manual dismissal replaced this timeout.
        }
    }

    private void DismissNotification()
    {
        _notificationCancellation?.Cancel();
        IsNotificationVisible = false;
    }

    private static string GetApplicationVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? typeof(MainViewModel).Assembly.GetName().Version?.ToString(3)
        ?? "1.2.0";
}
