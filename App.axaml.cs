using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Networking;
using ReceiptPrinterEmulator.Services;
using ReceiptPrinterEmulator.ViewModels;
using ReceiptPrinterEmulator.Views;

namespace ReceiptPrinterEmulator;

public partial class App : Application
{
    private NetServer? _server;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var printer = new ReceiptPrinter(PaperConfiguration.Default);

            _server = new NetServer(printer, 9100);
            _ = _server.Run();

            var notifications = new NotificationService();
            var viewModel = new MainWindowViewModel(printer, _server, notifications);

            var window = new MainWindow { DataContext = viewModel };
            notifications.AttachWindow(window);
            desktop.MainWindow = window;

            desktop.ShutdownRequested += (_, _) => _server.Stop();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
