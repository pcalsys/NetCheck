using NetCheck.App.Mvvm;

namespace NetCheck.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboard;
    private readonly HistoryViewModel _history;
    private readonly SettingsViewModel _settings;
    private ObservableObject _currentPage;
    private string _currentPageName = "Dashboard";

    public MainViewModel(
        DashboardViewModel dashboard,
        HistoryViewModel history,
        SettingsViewModel settings)
    {
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _currentPage = dashboard;

        ShowDashboardCommand = new RelayCommand(ShowDashboard);
        ShowHistoryCommand = new AsyncRelayCommand(ShowHistoryAsync);
        ShowSettingsCommand = new AsyncRelayCommand(ShowSettingsAsync);
    }

    public RelayCommand ShowDashboardCommand { get; }

    public AsyncRelayCommand ShowHistoryCommand { get; }

    public AsyncRelayCommand ShowSettingsCommand { get; }

    public ObservableObject CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string CurrentPageName
    {
        get => _currentPageName;
        private set => SetProperty(ref _currentPageName, value);
    }

    public Task InitializeAsync() => _dashboard.InitializeAsync();

    private void ShowDashboard()
    {
        CurrentPage = _dashboard;
        CurrentPageName = "Dashboard";
    }

    private async Task ShowHistoryAsync()
    {
        CurrentPage = _history;
        CurrentPageName = "History";
        await _history.LoadAsync().ConfigureAwait(true);
    }

    private async Task ShowSettingsAsync()
    {
        CurrentPage = _settings;
        CurrentPageName = "Settings";
        await _settings.LoadAsync().ConfigureAwait(true);
    }
}

