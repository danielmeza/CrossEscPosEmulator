using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CrossEscPos.Emulator;
using CrossEscPos.Rendering.Skia;
using CrossEscPos.Controls.Services;
using CrossEscPos.App.Desktop.ViewModels;
using CrossEscPos.App.Desktop.Views;

namespace CrossEscPos.App.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Composition root: pick the render backend (SkiaSharp) and wire it into the headless core.
            var imageFactory = new SkiaImageFactory();
            var typefaces = new SkiaTypefaceProvider();
            var encoder = new SkiaImageEncoder();

            var printer = new ReceiptPrinter(PaperConfiguration.Default, imageFactory, typefaces);
            // Marshal off-thread state mutations (e.g. a drawer kick over TCP) to the UI thread.
            printer.UiDispatch = a => Dispatcher.UIThread.Post(a);
            var notifications = new NotificationService();
            var dialogs = new FileDialogService();

            // Initial transport settings come from environment variables; the UI can change them at
            // runtime. ESCPOS_TCP_PORT ("off"/"0" disables auto-start), ESCPOS_SERIAL_PORT / _BAUD.
            var (tcpEnabled, tcpPort) = ReadTcpSettings();
            var serialPort = Environment.GetEnvironmentVariable("ESCPOS_SERIAL_PORT");
            int serialBaud = int.TryParse(Environment.GetEnvironmentVariable("ESCPOS_SERIAL_BAUD"), out var b) ? b : 9600;
            var listenAddress = Environment.GetEnvironmentVariable("ESCPOS_LISTEN_ADDRESS") ?? "0.0.0.0";

            var viewModel = new MainWindowViewModel(printer, encoder, notifications, dialogs,
                listenAddress, tcpPort, tcpEnabled,
                string.IsNullOrWhiteSpace(serialPort) ? null : serialPort, serialBaud);

            var window = new MainWindow { DataContext = viewModel };
            notifications.AttachWindow(window);
            dialogs.AttachTopLevel(window); // Window is a TopLevel
            desktop.MainWindow = window;

            desktop.ShutdownRequested += (_, _) => viewModel.Shutdown();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static (bool enabled, int port) ReadTcpSettings()
    {
        var setting = Environment.GetEnvironmentVariable("ESCPOS_TCP_PORT");
        if (setting is "off" or "none" or "disabled" or "0")
            return (false, 9100);

        int port = int.TryParse(setting, out var p) && p > 0 ? p : 9100;
        return (true, port);
    }
}
