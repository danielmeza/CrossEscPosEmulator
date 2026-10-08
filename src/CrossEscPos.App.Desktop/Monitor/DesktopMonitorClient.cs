using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using CrossEscPos.App.Monitor;
using CrossEscPos.App.Transports;
using CrossEscPos.Emulator;
using CrossEscPos.Transports;

namespace CrossEscPos.App.Desktop.Monitor;

/// <summary>
/// Desktop <see cref="IMonitorClient"/>: connects to the emulator/printer over TCP, serial, or USB,
/// enables Automatic Status Back on connect, and reports the printer's status as
/// <see cref="MonitorStatus"/>.
///
/// The transports do not share a base type: TCP and serial use ESC-POS-.NET's
/// <see cref="BasePrinter"/> family, which parses the status channel itself into
/// <see cref="PrinterStatusEventArgs"/>, while USB is this repository's own
/// <see cref="UsbPrinter"/>, which hands the raw 4-byte block over for
/// <see cref="AutoStatusBackReader"/> to decode. <see cref="IPrinterLink"/> is where the two meet.
/// </summary>
public sealed class DesktopMonitorClient : IMonitorClient
{
    private const string Tcp = "TCP", Serial = "Serial", Usb = "USB";

    private readonly EPSON _e = new();
    private IPrinterLink? _link;

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
                _link = new UsbLink(new UsbPrinter(dev.Vid, dev.Pid), RaiseStatus);
                target = dev.Display;
                break;

            case Serial:
                if (string.IsNullOrWhiteSpace(_serialPort.Value))
                    throw new InvalidOperationException("No serial port selected.");
                int baud = int.TryParse(_baud.Value, out var b) && b > 0 ? b : 9600;
                _link = new EscPosNetLink(
                    new SerialPrinter(portName: _serialPort.Value, baudRate: baud), RaiseStatus);
                target = $"serial {_serialPort.Value} @ {baud}";
                break;

            default: // TCP
                _link = new EscPosNetLink(new NetworkPrinter(new NetworkPrinterSettings
                {
                    ConnectionString = $"{_host.Value}:{_port.Value}",
                    PrinterName = "Monitor"
                }), RaiseStatus);
                target = $"{_host.Value}:{_port.Value}";
                break;
        }

        try
        {
            // Ask the emulator to push status on every state change (panel toggles show up here).
            _link.Write(_e.EnableAutomaticStatusBack());
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
        var link = _link ?? throw new InvalidOperationException("Not connected.");
        return Task.Run(() => link.Write(data));
    }

    public void Disconnect()
    {
        try { _link?.Dispose(); }
        catch { /* ignore */ }
        _link = null;
    }

    private void RaiseStatus(MonitorStatus status) => StatusReceived?.Invoke(status);

    /// <summary>The connected printer, whichever transport it arrived over.</summary>
    private interface IPrinterLink : IDisposable
    {
        void Write(byte[] data);
    }

    /// <summary>
    /// TCP and serial, over ESC-POS-.NET. The library owns the status channel and raises its own
    /// parsed <see cref="PrinterStatusEventArgs"/>, which is mapped onto the shared snapshot here.
    /// </summary>
    private sealed class EscPosNetLink : IPrinterLink
    {
        private readonly BasePrinter _printer;
        private readonly Action<MonitorStatus> _onStatus;

        public EscPosNetLink(BasePrinter printer, Action<MonitorStatus> onStatus)
        {
            _printer = printer;
            _onStatus = onStatus;
            _printer.StatusChanged += OnStatusChanged;
        }

        public void Write(byte[] data) => _printer.Write(data);

        private void OnStatusChanged(object? sender, EventArgs e)
        {
            if (e is not PrinterStatusEventArgs s)
                return;
            _onStatus(new MonitorStatus(
                Online: s.IsPrinterOnline == true,
                PaperOut: s.IsPaperOut == true,
                PaperLow: s.IsPaperLow == true,
                CoverOpen: s.IsCoverOpen == true,
                DrawerOpen: s.IsCashDrawerOpen == true,
                Error: s.IsInErrorState == true));
        }

        public void Dispose()
        {
            _printer.StatusChanged -= OnStatusChanged;
            _printer.Dispose();
        }
    }

    /// <summary>
    /// USB, over this repository's own transport. It frames the status channel into ASB blocks but
    /// leaves the meaning of the bits to <see cref="AutoStatusBackReader"/> — the same decode the
    /// browser head uses, so both heads report a given block identically.
    /// </summary>
    private sealed class UsbLink : IPrinterLink
    {
        private readonly UsbPrinter _printer;
        private readonly Action<MonitorStatus> _onStatus;

        public UsbLink(UsbPrinter printer, Action<MonitorStatus> onStatus)
        {
            _printer = printer;
            _onStatus = onStatus;
            _printer.StatusFrameReceived += OnStatusFrame;
        }

        public void Write(byte[] data) => _printer.Send(data);

        private void OnStatusFrame(byte[] frame)
        {
            if (MonitorStatus.From(AutoStatusBackReader.Parse(frame)) is { } status)
                _onStatus(status);
        }

        public void Dispose()
        {
            _printer.StatusFrameReceived -= OnStatusFrame;
            _printer.Dispose();
        }
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
