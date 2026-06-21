using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CrossEscPos.App.Desktop.ViewModels;
using CrossEscPos.Controls;

namespace CrossEscPos.App.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private MonitorWindow? _monitorWindow;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ReceiptsUpdated -= OnReceiptsUpdated;
            _viewModel.OpenMonitorRequested -= OnOpenMonitorRequested;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.ReceiptsUpdated += OnReceiptsUpdated;
            _viewModel.OpenMonitorRequested += OnOpenMonitorRequested;
        }
    }

    private void OnOpenMonitorRequested(object? sender, EventArgs e)
    {
        if (_monitorWindow is not null)
        {
            _monitorWindow.Activate();
            return;
        }

        _monitorWindow = new MonitorWindow
        {
            DataContext = new MonitorWindowViewModel(_viewModel?.CurrentTcpPort ?? 9100)
        };
        _monitorWindow.Closed += (_, _) => _monitorWindow = null;
        _monitorWindow.Show();
    }

    private void OnReceiptsUpdated(object? sender, EventArgs e)
    {
        // Defer so the ItemsControl has laid out the newly added item before we scroll.
        Dispatcher.UIThread.Post(() =>
        {
            this.FindControl<ReceiptView>("Receipts")?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }
}
