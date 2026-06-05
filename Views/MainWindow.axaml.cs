using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ReceiptPrinterEmulator.ViewModels;

namespace ReceiptPrinterEmulator.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

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
            _viewModel.ReceiptsUpdated -= OnReceiptsUpdated;

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
            _viewModel.ReceiptsUpdated += OnReceiptsUpdated;
    }

    private void OnReceiptsUpdated(object? sender, EventArgs e)
    {
        // Defer so the ItemsControl has laid out the newly added item before we scroll.
        Dispatcher.UIThread.Post(() =>
        {
            this.FindControl<ScrollViewer>("MainScrollView")?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }
}
