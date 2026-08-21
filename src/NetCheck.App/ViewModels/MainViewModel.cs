using NetCheck.App.Mvvm;
using NetCheck.App.Localization;
using NetCheck.Core.Abstractions;
using NetCheck.Infrastructure.Logging;

namespace NetCheck.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboard;
    private readonly HistoryViewModel _history;
    private readonly SettingsViewModel _settings;
    private readonly ISettingsStore _settingsStore;
    private readonly LocalizationService _text;
    private readonly FileLogger _logger;
    private ObservableObject _currentPage;
    private MainPage _selectedPage = MainPage.Dashboard;
    private string _menuLanguage = "en";

    public MainViewModel(
        DashboardViewModel dashboard,
        HistoryViewModel history,
        SettingsViewModel settings,
        ISettingsStore settingsStore,
        LocalizationService text,
        FileLogger logger)
    {
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentPage = dashboard;

        ShowDashboardCommand = new RelayCommand(ShowDashboard);
        ShowHistoryCommand = new AsyncRelayCommand(ShowHistoryAsync);
        ShowSettingsCommand = new AsyncRelayCommand(ShowSettingsAsync);
        UseEnglishMenuCommand = new AsyncRelayCommand(() => SetMenuLanguageAsync("en"));
        UseGermanMenuCommand = new AsyncRelayCommand(() => SetMenuLanguageAsync("de"));
    }

    public RelayCommand ShowDashboardCommand { get; }

    public AsyncRelayCommand ShowHistoryCommand { get; }

    public AsyncRelayCommand ShowSettingsCommand { get; }

    public AsyncRelayCommand UseEnglishMenuCommand { get; }

    public AsyncRelayCommand UseGermanMenuCommand { get; }

    public ObservableObject CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string CurrentPageName => _selectedPage switch
    {
        MainPage.History => _text.Translate("History"),
        MainPage.Settings => _text.Translate("Settings"),
        _ => _text.Translate("Dashboard")
    };

    public string DashboardMenuLabel => _text.Translate("Dashboard");

    public string HistoryMenuLabel => _text.Translate("History");

    public string SettingsMenuLabel => _text.Translate("Settings");

    public string MenuLanguageLabel => _text.Translate("LANGUAGE");

    public string NavigationLabel => _text.Translate("NAVIGATION");

    public string CurrentLanguageTag => _text.Culture.IetfLanguageTag;

    public bool IsDashboardSelected => _selectedPage == MainPage.Dashboard;

    public bool IsHistorySelected => _selectedPage == MainPage.History;

    public bool IsSettingsSelected => _selectedPage == MainPage.Settings;

    public bool IsEnglishMenu => !IsGermanMenu;

    public bool IsGermanMenu => string.Equals(_menuLanguage, "de", StringComparison.Ordinal);

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

        ApplyMenuLanguage(normalizedLanguage);
        try
        {
            var settings = await _settingsStore.LoadAsync().ConfigureAwait(true);
            await _settingsStore.SaveAsync(settings with { MenuLanguage = normalizedLanguage }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _logger.Error("Could not save the menu language preference.", exception);
        }
    }

    private void ApplyMenuLanguage(string language)
    {
        _menuLanguage = string.Equals(language, "de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
        _text.SetLanguage(_menuLanguage);
        _dashboard.RefreshLocalization();
        _history.RefreshLocalization();
        _settings.RefreshLocalization();
        OnPropertiesChanged(
            nameof(CurrentPageName),
            nameof(DashboardMenuLabel),
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
            nameof(IsHistorySelected),
            nameof(IsSettingsSelected));
    }

    private enum MainPage
    {
        Dashboard,
        History,
        Settings
    }
}
