using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using CrossEscPos.App.Browser.Transports;
using CrossEscPos.App.Monitor;
using CrossEscPos.App.Transports;
using CrossEscPos.Bridge;
using CrossEscPos.Transports.Browser;

namespace CrossEscPos.App.Browser.Monitor;

/// <summary>
/// The browser Monitor, with the same transports as the desktop one — pick a transport and drive a
/// printer: <b>Network (TCP)</b> (the host dials a real network printer on the browser's behalf through
/// the SignalR proxy), <b>Web Serial</b>, <b>WebUSB</b>, or the <b>SignalR proxy</b> round-trip to the
/// in-page emulator. Serial/USB reuse the shared JS bridge on their own sender channels
/// (<c>mon-serial</c>/<c>mon-usb</c>) so they don't clash with the emulator's connections.
/// </summary>
public sealed class WebMonitorClient : IMonitorClient
{
    private const string Proxy = "SignalR proxy", Tcp = "Network (TCP)", Serial = "Web Serial", Usb = "WebUSB";

    private readonly IJsTransportBridge _bridge;
    private readonly string _hubUrl;
    private readonly TransportField _url;
    private readonly TransportField _host = new("Host", "127.0.0.1");
    private readonly TransportField _port = new("Port", "9100");
    private readonly TransportField _baud = new("Baud", "9600");
    private readonly TransportField _usbDevice = new("Device (VID:PID)", "", new[] { "" });

    private HubConnection? _hub;
    private IBridgeServer? _server;
    private string _mode = Proxy;

    public WebMonitorClient(IJsTransportBridge bridge)
    {
        _bridge = bridge;
        _bridge.DataReceived += OnBridgeData;

        var origin = WasmJsTransportBridge.PageOrigin();
        _hubUrl = (string.IsNullOrEmpty(origin) ? "http://localhost:5000" : origin) + "/bridge";
        _url = new TransportField("Proxy URL", _hubUrl);
    }

    public IReadOnlyList<string> Modes { get; } = new[] { Proxy, Tcp, Serial, Usb };

    public string Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
                return;
            _mode = value;
            FieldsChanged?.Invoke();
        }
    }

    public IReadOnlyList<TransportField> Fields => _mode switch
    {
        Tcp => new[] { _host, _port },
        Serial => new[] { _baud },
        Usb => new[] { _usbDevice },
        _ => new[] { _url },
    };

    public bool CanRefresh => _mode == Usb;

    public event Action? FieldsChanged;
    public event Action<MonitorStatus>? StatusReceived;
    public event Action<string>? Log;

    private bool IsHubMode => _mode is Proxy or Tcp;
    private string KindId => _mode == Usb ? "mon-usb" : "mon-serial";

    public async Task RefreshAsync()
    {
        if (_mode != Usb)
            return;
        var current = _usbDevice.Value;
        var devices = await _bridge.ListUsbDevicesAsync();
        var options = _usbDevice.Options!;
        options.Clear();
        foreach (var d in devices)
            options.Add(d);
        _usbDevice.Value = current is not null && devices.Contains(current) ? current : devices.FirstOrDefault() ?? "";
        if (devices.Count == 0)
            Log?.Invoke("No paired USB devices — click Connect to pair one.");
    }

    public async Task<string> ConnectAsync()
    {
        if (IsHubMode)
        {
            await StartHubAsync(_mode == Proxy ? _url.Value : _hubUrl);
            if (_mode == Tcp)
            {
                int port = int.TryParse(_port.Value, out var p) ? p : 9100;
                await _server!.ConnectTcp(_host.Value, port);
                return $"{_host.Value}:{port}";
            }
            return _url.Value;
        }

        // Serial: pass the baud; USB: pass the selected device's index (or none → the picker).
        string? options = _mode == Serial ? _baud.Value : IndexOf(_usbDevice);
        var description = await _bridge.ConnectAsync(KindId, options);
        if (string.IsNullOrEmpty(description))
            throw new InvalidOperationException("Connection cancelled.");
        return description;
    }

    private async Task StartHubAsync(string url)
    {
        var hub = new HubConnectionBuilder().WithUrl(url).WithAutomaticReconnect().Build();
        hub.On<byte[]>(nameof(IBridgeClient.ReceiveStatus), OnStatusBytes);
        hub.Closed += _ => { Log?.Invoke("proxy connection closed"); return Task.CompletedTask; };
        await hub.StartAsync();
        _hub = hub;
        _server = new BridgeServerProxy(hub);
    }

    private string? IndexOf(TransportField dropdown)
    {
        var options = dropdown.Options;
        if (options is null)
            return null;
        var i = options.IndexOf(dropdown.Value);
        return i >= 0 ? i.ToString() : null; // null → the JS side opens the device picker
    }

    public async Task SendAsync(byte[] data)
    {
        if (IsHubMode)
        {
            var server = _server ?? throw new InvalidOperationException("Not connected.");
            await server.SendToEmulator(data); // routed to the emulator, or the TCP printer if one is open
        }
        else
        {
            await _bridge.WriteAsync(KindId, data);
        }
    }

    public void Disconnect()
    {
        if (IsHubMode)
        {
            var hub = _hub;
            _hub = null;
            _server = null;
            if (hub is not null)
                _ = hub.DisposeAsync();
        }
        else
        {
            _ = _bridge.DisconnectAsync(KindId);
        }
    }

    private void OnBridgeData(string kind, byte[] data)
    {
        if (!IsHubMode && kind == KindId)
            OnStatusBytes(data);
    }

    private void OnStatusBytes(byte[] data)
    {
        var status = MonitorStatus.FromAutoStatusBack(data);
        if (status is not null)
            StatusReceived?.Invoke(status);
    }
}
