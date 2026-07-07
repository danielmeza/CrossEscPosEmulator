using System;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using CrossEscPos.App.Transports;
using CrossEscPos.Emulator;
using CrossEscPos.Transports;

namespace CrossEscPos.App.Desktop.Transports;

/// <summary>The desktop serial reader (RS-232 / USB-serial), refreshable port list.</summary>
public sealed class SerialTransportEntry : TransportEntry
{
    private readonly SerialServer _serial;
    private readonly TransportField _port;
    private readonly TransportField _baud;

    public SerialTransportEntry(ReceiptPrinter printer, string? port, int baud, bool autoStart) : base("Serial")
    {
        _serial = new SerialServer(printer);
        var ports = Ports();
        _port = new TransportField("Port", port ?? (ports.Length > 0 ? ports[0] : string.Empty), ports);
        _baud = new TransportField("Baud rate", baud.ToString());
        Fields = new[] { _port, _baud };
        Set(false, "Serial: closed");
        if (autoStart)
            StartCore();
    }

    public override bool CanRefresh => true;
    public override string ButtonText => IsActive ? "Close" : "Open";

    protected override Task ToggleAsync()
    {
        if (_serial.IsRunning) { _serial.Stop(); Set(false, "Serial: closed"); }
        else StartCore();
        return Task.CompletedTask;
    }

    private void StartCore()
    {
        if (string.IsNullOrWhiteSpace(_port.Value)) { Status = "No port selected"; return; }
        int baud = int.TryParse(_baud.Value, out var b) && b > 0 ? b : 9600;
        _serial.Start(_port.Value, baud);
        Set(true, $"{_serial.PortName} @ {_serial.BaudRate} (open)");
    }

    protected override Task RefreshAsync()
    {
        var options = _port.Options!;
        var current = _port.Value;
        options.Clear();
        foreach (var name in Ports())
            options.Add(name);
        _port.Value = current is not null && options.Contains(current) ? current : options.FirstOrDefault() ?? string.Empty;
        return Task.CompletedTask;
    }

    public override void Shutdown() => _serial.Stop();

    private static string[] Ports()
    {
        try { return SerialPort.GetPortNames().OrderBy(n => n).ToArray(); }
        catch { return Array.Empty<string>(); }
    }
}
