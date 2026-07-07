using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using CrossEscPos.App.Transports;
using CrossEscPos.Emulator;
using CrossEscPos.Logging;
using CrossEscPos.Transports;

namespace CrossEscPos.App.Desktop.Transports;

/// <summary>The desktop TCP/IP listener (a network receipt printer on port 9100 by convention).</summary>
public sealed class TcpTransportEntry : TransportEntry
{
    private readonly NetServer _tcp;
    private readonly TransportField _address;
    private readonly TransportField _port;

    public TcpTransportEntry(ReceiptPrinter printer, string address, int port, bool autoStart) : base("TCP/IP")
    {
        _tcp = new NetServer(printer);
        _address = new TransportField("Listen address", address, Addresses(address));
        _port = new TransportField("Port", port.ToString());
        Fields = new[] { _address, _port };
        Set(false, "TCP stopped");
        if (autoStart)
            StartCore();
    }

    public override string ButtonText => IsActive ? "Stop" : "Start";

    public int CurrentPort => int.TryParse(_port.Value, out var p) ? p : 9100;

    protected override Task ToggleAsync()
    {
        if (_tcp.IsRunning) { _tcp.Stop(); Set(false, "TCP stopped"); }
        else StartCore();
        return Task.CompletedTask;
    }

    private void StartCore()
    {
        if (!IPAddress.TryParse(_address.Value, out var addr)) { Status = $"Invalid address '{_address.Value}'"; return; }
        if (!int.TryParse(_port.Value, out var port) || port is < 1 or > 65535) { Status = $"Invalid port '{_port.Value}'"; return; }
        _tcp.Start(addr, port);
        Set(true, $"Listening on {_tcp.EndPoint}");
    }

    public override void Shutdown() => _tcp.Stop();

    private static string[] Addresses(string preferred)
    {
        var list = new List<string> { "0.0.0.0", "127.0.0.1" };
        try
        {
            foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                if (ip.AddressFamily == AddressFamily.InterNetwork && !list.Contains(ip.ToString()))
                    list.Add(ip.ToString());
        }
        catch (Exception ex) { Logger.Exception(ex, "Failed to enumerate local addresses"); }
        if (!string.IsNullOrWhiteSpace(preferred) && !list.Contains(preferred))
            list.Add(preferred);
        return list.ToArray();
    }
}
