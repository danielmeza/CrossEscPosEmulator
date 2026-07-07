using System;
using System.Collections.Generic;
using Avalonia.Controls;
using CrossEscPos.App;
using CrossEscPos.App.Desktop.Transports;
using CrossEscPos.App.Desktop.ViewModels;
using CrossEscPos.App.Desktop.Views;
using CrossEscPos.App.Transports;
using CrossEscPos.Controls.Services;
using CrossEscPos.Emulator;
using CrossEscPos.Graphics;

namespace CrossEscPos.App.Desktop;

/// <summary>
/// The desktop platform: native file dialogs + sound/flash notifications, TCP + serial transports, and
/// the Monitor test-client window. Injected into the shared <see cref="CrossEscPos.App.App"/>.
/// </summary>
public sealed class DesktopPlatformServices : IPlatformServices
{
    private readonly RenderBackend _backend;
    private readonly FileDialogService _dialogs = new();
    private readonly NotificationService _notifications = new();
    private TcpTransportEntry? _tcp;
    private MonitorWindow? _monitorWindow;

    public DesktopPlatformServices(RenderBackend backend) => _backend = backend;

    public IReceiptImageFactory ImageFactory => _backend.ImageFactory;
    public ITypefaceProvider Typefaces => _backend.Typefaces;
    public IImageEncoder Encoder => _backend.Encoder;
    public string BackendName => _backend.Name;
    public IFileDialogService FileDialogs => _dialogs;
    public INotificationService Notifications => _notifications;
    public byte[] SampleTicket => Sample.Ticket;

    public IReadOnlyList<TransportEntry> CreateTransports(ReceiptPrinter printer)
    {
        var (tcpEnabled, tcpPort) = ReadTcpSettings();
        var serialPort = Environment.GetEnvironmentVariable("ESCPOS_SERIAL_PORT");
        int serialBaud = int.TryParse(Environment.GetEnvironmentVariable("ESCPOS_SERIAL_BAUD"), out var b) ? b : 9600;
        var listenAddress = Environment.GetEnvironmentVariable("ESCPOS_LISTEN_ADDRESS") ?? "0.0.0.0";

        _tcp = new TcpTransportEntry(printer, listenAddress, tcpPort, tcpEnabled);
        var serial = new SerialTransportEntry(printer,
            string.IsNullOrWhiteSpace(serialPort) ? null : serialPort, serialBaud, autoStart: serialPort is not null);
        return new TransportEntry[] { _tcp, serial };
    }

    public bool SupportsMonitor => true;

    public void OpenMonitor()
    {
        if (_monitorWindow is not null)
        {
            _monitorWindow.Activate();
            return;
        }
        _monitorWindow = new MonitorWindow
        {
            DataContext = new MonitorWindowViewModel(_tcp?.CurrentPort ?? 9100)
        };
        _monitorWindow.Closed += (_, _) => _monitorWindow = null;
        _monitorWindow.Show();
    }

    public void AttachRoot(Control mainView) => _dialogs.AttachControl(mainView);

    public Window CreateMainWindow(Control content)
    {
        var window = new MainWindow
        {
            Content = content,
            Title = $"ESC/POS Receipt Printer Emulator  •  Render: {_backend.Name}"
        };
        _notifications.AttachWindow(window);
        _dialogs.AttachTopLevel(window);
        return window;
    }

    public void Shutdown() { /* transports are stopped by MainViewModel.Shutdown */ }

    private static (bool enabled, int port) ReadTcpSettings()
    {
        var setting = Environment.GetEnvironmentVariable("ESCPOS_TCP_PORT");
        if (setting is "off" or "none" or "disabled" or "0")
            return (false, 9100);
        int port = int.TryParse(setting, out var p) && p > 0 ? p : 9100;
        return (true, port);
    }
}
