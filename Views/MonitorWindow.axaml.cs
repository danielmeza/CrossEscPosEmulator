using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReceiptPrinterEmulator.ViewModels;

namespace ReceiptPrinterEmulator.Views;

public partial class MonitorWindow : Window
{
    public MonitorWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as MonitorWindowViewModel)?.Shutdown();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
