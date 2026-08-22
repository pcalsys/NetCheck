using System.ComponentModel;
using System.Windows;
using NetCheck.App.ViewModels;

namespace NetCheck.App;

public partial class MainWindow : Window
{
    private bool _shutdownCompleted;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.InitializeAsync().ConfigureAwait(true);
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_shutdownCompleted || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        e.Cancel = true;
        IsEnabled = false;
        try
        {
            await viewModel.ShutdownAsync().ConfigureAwait(true);
        }
        finally
        {
            _shutdownCompleted = true;
            Closing -= OnClosing;
            IsEnabled = true;
            _ = Dispatcher.BeginInvoke(Close);
        }
    }
}
