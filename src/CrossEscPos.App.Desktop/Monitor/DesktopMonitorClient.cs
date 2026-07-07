using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using CrossEscPos.App.Monitor;
using CrossEscPos.App.Transports;
using CrossEscPos.Transports;

namespace CrossEscPos.App.Desktop.Monitor;

/// <summary>
/// Desktop <see cref="IMonitorClient"/>: connects to the emulator/printer over TCP, serial, or USB using
/// ESC-POS-.NET's <see cref="BasePrinter"/> family. Enables Automatic Status Back on connect and maps the
/// library's parsed <see cref="PrinterStatusEventArgs"/> onto the shared <see cref="MonitorStatus"/>.
/// </summary>
public sealed class DesktopMonitorClient : IMonitorClient
{
    private const string Tcp = "TCP", Serial = "Serial", Usb = "USB";

    private readonly EPSON _e = new();
    private BasePrinter? _printer;

    // TCP fields.
    private readonly TransportField _host = new("Host", "127.0.0.1");
    private readonly TransportField _port;
    // Serial fields.
    private readonly TransportField _serialPort = new("Port", "", new[] { "" });
    private readonly TransportField _baud = new("Baud", "9600");
    // USB fields.
    private readonly TransportField _usbDevice = new("Device", "", new[] { "" });
    private readonly Dictionary<string, UsbDeviceInfo> _usbByDisplay = new();

    private string _mode = Tcp;

    public DesktopMonitorClient(int defaultPort)
    {
        _port = new TransportField("Port", defaultPort.ToString());
        RefreshPorts();
    }

    public IReadOnlyList<string> Modes { get; } = new[] { Tcp, Serial, Usb };

    public string Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
                return;
            _mode = value;
            if (_mode == Usb)
                RefreshUsb();
            FieldsChanged?.Invoke();
        }
    }

    public IReadOnlyList<TransportField> Fields => _mode switch
    {
        Serial => new[] { _serialPort, _baud },
        Usb => new[] { _usbDevice },
        _ => new[] { _host, _port },
    };

    public bool CanRefresh => _mode is Serial or Usb;

    public event Action? FieldsChanged;
    public event Action<MonitorStatus>? StatusReceived;
    public event Action<string>? Log;

    public Task RefreshAsync()
    {
        if (_mode == Serial) RefreshPorts();
        else if (_mode == Usb) RefreshUsb();
        return Task.CompletedTask;
    }

    private void RefreshPorts()
    {
        var current = _serialPort.Value;
        var names = Array.Empty<string>();
        try { names = SerialPort.GetPortNames().OrderBy(n => n).ToArray(); }
        catch (Exception ex) { Log?.Invoke($"could not list serial ports: {ex.Message}"); }
        SetOptions(_serialPort, names, current);
    }

    private void RefreshUsb()
    {
        var current = _usbDevice.Value;
        _usbByDisplay.Clear();
        try
        {
            foreach (var d in UsbPrinter.ListDevices())
                _usbByDisplay[d.Display] = d;
        }
        catch (Exception ex)
        {
            if (IsLibusbMissing(ex)) AppendLibusbHelp();
            else Log?.Invoke($"USB list failed: {ex.Message}");
        }
        SetOptions(_usbDevice, _usbByDisplay.Keys.ToArray(), current);
    }

    private static void SetOptions(TransportField field, string[] options, string? keep)
    {
        var opts = field.Options!; // dropdown fields are constructed with a non-null Options collection
        opts.Clear();
        foreach (var o in options)
            opts.Add(o);
        field.Value = keep is not null && options.Contains(keep) ? keep : options.FirstOrDefault() ?? "";
    }

    public Task<string> ConnectAsync()
    {
        string target;
        switch (_mode)
        {
            case Usb:
                if (!_usbByDisplay.TryGetValue(_usbDevice.Value ?? "", out var dev))
                    throw new InvalidOperationException("No USB device selected.");
                _printer = new UsbPrinter(dev.Vid, dev.Pid);
                target = dev.Display;
                break;

            case Serial:
                if (string.IsNullOrWhiteSpace(_serialPort.Value))
                    throw new InvalidOperationException("No serial port selected.");
                int baud = int.TryParse(_baud.Value, out var b) && b > 0 ? b : 9600;
                _printer = new SerialPrinter(portName: _serialPort.Value, baudRate: baud);
                target = $"serial {_serialPort.Value} @ {baud}";
                break;

            default: // TCP
                _printer = new NetworkPrinter(new NetworkPrinterSettings
                {
                    ConnectionString = $"{_host.Value}:{_port.Value}",
                    PrinterName = "Monitor"
                });
                target = $"{_host.Value}:{_port.Value}";
                break;
        }

        try
        {
            _printer.StatusChanged += OnStatusChanged;
            // Ask the emulator to push status on every state change (panel toggles show up here).
            _printer.Write(_e.EnableAutomaticStatusBack());
        }
        catch (Exception ex)
        {
            if (_mode == Usb && IsLibusbMissing(ex))
                AppendLibusbHelp();
            Disconnect();
            throw;
        }

        return Task.FromResult(target);
    }

    public Task SendAsync(byte[] data)
    {
        var printer = _printer ?? throw new InvalidOperationException("Not connected.");
        return Task.Run(() => printer.Write(data));
    }

    public void Disconnect()
    {
        try
        {
            if (_printer is not null)
            {
                _printer.StatusChanged -= OnStatusChanged;
                _printer.Dispose();
            }
        }
        catch { /* ignore */ }
        _printer = null;
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        if (e is not PrinterStatusEventArgs s)
            return;
        StatusReceived?.Invoke(new MonitorStatus(
            Online: s.IsPrinterOnline == true,
            PaperOut: s.IsPaperOut == true,
            PaperLow: s.IsPaperLow == true,
            CoverOpen: s.IsCoverOpen == true,
            DrawerOpen: s.IsCashDrawerOpen == true,
            Error: s.IsInErrorState == true));
    }

    /// <summary>True when an exception was caused by libusb not being loadable.</summary>
    private static bool IsLibusbMissing(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is DllNotFoundException || e.Message.Contains("libusb", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void AppendLibusbHelp()
    {
        string install = OperatingSystem.IsMacOS()
            ? "brew install libusb"
            : OperatingSystem.IsLinux()
                ? "sudo apt install libusb-1.0-0   (Debian/Ubuntu) — or your distro's libusb-1.0 package"
                : "libusb ships with the app on Windows; reinstall the app if it's missing";
        Log?.Invoke("Could not find libusb (the native USB library) — USB printing is unavailable.\n" +
                    $"  Install it:  {install}\n" +
                    "  Then click the ⟳ button to refresh the USB device list.");
    }
}
