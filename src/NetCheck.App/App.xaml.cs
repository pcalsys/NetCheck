using System.Windows;
using System.Windows.Threading;
using NetCheck.App.Localization;
using NetCheck.App.Services;
using NetCheck.App.ViewModels;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Diagnostics;
using NetCheck.Infrastructure.Diagnostics;
using NetCheck.Infrastructure.Export;
using NetCheck.Infrastructure.Logging;
using NetCheck.Infrastructure.Network;
using NetCheck.Infrastructure.Storage;

namespace NetCheck.App;

public partial class App : Application
{
    private JsonReportHistoryStore? _historyStore;
    private JsonSettingsStore? _settingsStore;
    private FileLogger? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (WindowsNetworkRepairService.IsHelperInvocation(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var exitCode = await WindowsNetworkRepairService
                .RunElevatedHelperAsync(e.Args)
                .ConfigureAwait(true);
            Shutdown(exitCode);
            return;
        }

        var paths = new AppDataPaths();
        _logger = new FileLogger(paths.LogFile);
        _historyStore = new JsonReportHistoryStore(paths);
        _settingsStore = new JsonSettingsStore(paths);
        var localization = new LocalizationService();
        var initialSettings = await _settingsStore.LoadAsync().ConfigureAwait(true);
        localization.SetLanguage(initialSettings.MenuLanguage);
        var reportLocalization = new ReportLocalizationService(localization);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        IReadOnlyList<IDiagnosticCheck> checks =
        [
            new AdapterDiagnosticCheck(),
            new IpConfigurationDiagnosticCheck(),
            new GatewayDiagnosticCheck(),
            new DnsDiagnosticCheck(),
            new InternetReachabilityDiagnosticCheck(),
            new WebConnectivityDiagnosticCheck(),
            new StabilityDiagnosticCheck(),
            new ProxyDiagnosticCheck()
        ];

        var engine = new DiagnosticEngine(
            checks,
            new WindowsNetworkSnapshotProvider(),
            new DiagnosisAnalyzer());
        var exporter = new ReportExporter(localization);
        var repairPlanner = new NetworkRepairPlanner();
        var repairService = new WindowsNetworkRepairService();
        var dialogService = new FileDialogService(localization);
        var messageService = new MessageService();

        var dashboard = new DashboardViewModel(
            engine,
            _historyStore,
            exporter,
            _settingsStore,
            repairPlanner,
            repairService,
            dialogService,
            messageService,
            localization,
            reportLocalization,
            _logger);
        var history = new HistoryViewModel(
            _historyStore,
            exporter,
            _settingsStore,
            dialogService,
            messageService,
            localization,
            reportLocalization,
            _logger);
        var settings = new SettingsViewModel(_settingsStore, messageService, localization, _logger);
        var mainViewModel = new MainViewModel(
            dashboard,
            history,
            settings,
            _settingsStore,
            localization,
            _logger);

        var window = new MainWindow
        {
            DataContext = mainViewModel
        };
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _historyStore?.Dispose();
        _settingsStore?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error("Unhandled UI exception.", e.Exception);
        var text = LocalizationService.Current;
        MessageBox.Show(
            text?.Translate("NetCheck encountered an unexpected error. The error was logged locally. Repairs never run without your approval.")
                ?? "NetCheck encountered an unexpected error. The error was logged locally. Repairs never run without your approval.",
            text?.Translate("NetCheck error") ?? "NetCheck error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger?.Error("Unhandled application exception.", exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.Error("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }
}
