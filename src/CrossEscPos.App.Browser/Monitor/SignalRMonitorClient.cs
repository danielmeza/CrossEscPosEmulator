using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using CrossEscPos.App.Browser.Transports;
using CrossEscPos.App.Monitor;
using CrossEscPos.App.Transports;
using CrossEscPos.Bridge;
using CrossEscPos.Transports.Browser;

namespace CrossEscPos.App.Browser.Monitor;

/// <summary>
/// Browser <see cref="IMonitorClient"/>: the "sender" side of the SignalR broker. It pushes ESC/POS jobs
/// to the emulator through the hub (<c>SendToEmulator</c>) and receives the emulator's status replies
/// (<c>ReceiveStatus</c>), which it parses as Automatic Status Back into a <see cref="MonitorStatus"/>.
/// This gives the in-browser Monitor a real round-trip against the in-browser emulator over one host.
/// </summary>
public sealed class SignalRMonitorClient : IMonitorClient
{
    private readonly TransportField _url;
    private HubConnection? _hub;
    private IBridgeServer? _server;
    private string _mode = "SignalR proxy";

    public SignalRMonitorClient()
    {
        var origin = WasmJsTransportBridge.PageOrigin();
        var bridgeUrl = (string.IsNullOrEmpty(origin) ? "http://localhost:5000" : origin) + "/bridge";
        _url = new TransportField("Proxy URL", bridgeUrl);
    }

    public IReadOnlyList<string> Modes { get; } = new[] { "SignalR proxy" };

    public string Mode
    {
        get => _mode;
        set => _mode = value;
    }

    public IReadOnlyList<TransportField> Fields => new[] { _url };
    public bool CanRefresh => false;

    // The proxy has a single mode with a fixed field, so this never fires — required by the interface.
#pragma warning disable CS0067
    public event Action? FieldsChanged;
#pragma warning restore CS0067
    public event Action<MonitorStatus>? StatusReceived;
    public event Action<string>? Log;

    public Task RefreshAsync() => Task.CompletedTask;

    public async Task<string> ConnectAsync()
    {
        var hub = new HubConnectionBuilder().WithUrl(_url.Value).WithAutomaticReconnect().Build();
        hub.On<byte[]>(nameof(IBridgeClient.ReceiveStatus), data =>
        {
            var status = MonitorStatus.FromAutoStatusBack(data);
            if (status is not null)
                StatusReceived?.Invoke(status);
        });
        hub.Closed += _ => { Log?.Invoke("proxy connection closed"); return Task.CompletedTask; };

        await hub.StartAsync();
        _hub = hub;
        _server = new BridgeServerProxy(hub);
        return _url.Value;
    }

    public async Task SendAsync(byte[] data)
    {
        var server = _server ?? throw new InvalidOperationException("Not connected.");
        await server.SendToEmulator(data);
    }

    public void Disconnect()
    {
        var hub = _hub;
        _hub = null;
        _server = null;
        if (hub is not null)
            _ = hub.DisposeAsync();
    }
}
