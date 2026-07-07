using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
/// The browser Monitor, with the same multi-transport shape as the desktop one — you pick a transport and
/// drive a printer with it: <b>SignalR proxy</b> (round-trip to the in-page emulator through the host), a
/// real <b>Web Serial</b> device, or a real <b>WebUSB</b> device. Serial/USB reuse the shared JS bridge on
/// their own sender channels (<c>mon-serial</c>/<c>mon-usb</c>) so they don't clash with the emulator's
/// connections. Device selection is the browser's native picker (Web Serial / WebUSB), and paired USB
/// devices can be listed by VID:PID.
/// </summary>
public sealed class WebMonitorClient : IMonitorClient
{
    private const string Proxy = "SignalR proxy", Serial = "Web Serial", Usb = "WebUSB";

    private readonly IJsTransportBridge _bridge;
    private readonly TransportField _url;
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
        _url = new TransportField("Proxy URL", (string.IsNullOrEmpty(origin) ? "http://localhost:5000" : origin) + "/bridge");
    }

    public IReadOnlyList<string> Modes { get; } = new[] { Proxy, Serial, Usb };

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
        Serial => new[] { _baud },
        Usb => new[] { _usbDevice },
        _ => new[] { _url },
    };

    public bool CanRefresh => _mode == Usb;

    public event Action? FieldsChanged;
    public event Action<MonitorStatus>? StatusReceived;
    public event Action<string>? Log;

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
        if (_mode == Proxy)
            return await ConnectProxyAsync();

        // Serial: pass the baud; USB: pass the selected device's index (or none → the picker).
        string? options = _mode == Serial
            ? _baud.Value
            : IndexOf(_usbDevice);
        var description = await _bridge.ConnectAsync(KindId, options);
        if (string.IsNullOrEmpty(description))
            throw new InvalidOperationException("Connection cancelled.");
        return description;
    }

    private string? IndexOf(TransportField dropdown)
    {
        var options = dropdown.Options;
        if (options is null)
            return null;
        var i = options.IndexOf(dropdown.Value);
        return i >= 0 ? i.ToString() : null; // null → the JS side opens the device picker
    }

    private async Task<string> ConnectProxyAsync()
    {
        var hub = new HubConnectionBuilder().WithUrl(_url.Value).WithAutomaticReconnect().Build();
        hub.On<byte[]>(nameof(IBridgeClient.ReceiveStatus), OnStatusBytes);
        hub.Closed += _ => { Log?.Invoke("proxy connection closed"); return Task.CompletedTask; };
        await hub.StartAsync();
        _hub = hub;
        _server = new BridgeServerProxy(hub);
        return _url.Value;
    }

    public async Task SendAsync(byte[] data)
    {
        if (_mode == Proxy)
        {
            var server = _server ?? throw new InvalidOperationException("Not connected.");
            await server.SendToEmulator(data);
        }
        else
        {
            await _bridge.WriteAsync(KindId, data);
        }
    }

    public void Disconnect()
    {
        if (_mode == Proxy)
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
        if (_mode != Proxy && kind == KindId)
            OnStatusBytes(data);
    }

    private void OnStatusBytes(byte[] data)
    {
        var status = MonitorStatus.FromAutoStatusBack(data);
        if (status is not null)
            StatusReceived?.Invoke(status);
    }
}
