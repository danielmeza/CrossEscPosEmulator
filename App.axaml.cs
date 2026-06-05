using System;
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
    private SerialServer? _serial;

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

            // Optional serial transport — enabled by setting ESCPOS_SERIAL_PORT
            // (e.g. /dev/ttys005 from a socat PTY pair, or COM3). Baud via ESCPOS_SERIAL_BAUD.
            _serial = TryCreateSerial(printer);
            if (_serial is not null)
                _ = _serial.Run();

            var notifications = new NotificationService();
            var viewModel = new MainWindowViewModel(printer, _server, notifications, _serial);

            var window = new MainWindow { DataContext = viewModel };
            notifications.AttachWindow(window);
            desktop.MainWindow = window;

            desktop.ShutdownRequested += (_, _) =>
            {
                _server.Stop();
                _serial?.Stop();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static SerialServer? TryCreateSerial(ReceiptPrinter printer)
    {
        var portName = Environment.GetEnvironmentVariable("ESCPOS_SERIAL_PORT");
        if (string.IsNullOrWhiteSpace(portName))
            return null;

        int baud = int.TryParse(Environment.GetEnvironmentVariable("ESCPOS_SERIAL_BAUD"), out var b) ? b : 9600;
        return new SerialServer(printer, portName, baud);
    }
}
