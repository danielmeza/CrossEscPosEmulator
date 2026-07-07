using System;
using System.Threading.Tasks;
using CrossEscPos.Bridge;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrossEscPos.Transports.Browser;

/// <summary>
/// Receives ESC/POS over a <b>SignalR</b> hub (the CrossEscPos.Host broker). SignalR's client runs in
/// WASM over the browser WebSocket, so this works in the Avalonia browser head and on desktop alike. The
/// emulator attaches as the "printer" and asks the proxy to open a TCP listener on <see cref="ListenAddress"/>:
/// <see cref="ListenPort"/> for this session; POS software connects there and the proxy relays jobs to it
/// and its status replies back. This is the TCP-in-the-browser path the sandbox otherwise forbids.
/// </summary>
public sealed class SignalRTransport : IReceiptTransport
{
    private readonly ITransportSink _sink;
    private HubConnection? _hub;
    private IBridgeServer? _server;

    public SignalRTransport(ITransportSink sink, string kind)
    {
        _sink = sink;
        Kind = kind;
    }

    public string Kind { get; }
    public string? Description { get; private set; }
    public bool IsConnected { get; private set; }
    public event Action? StateChanged;

    /// <summary>The proxy hub endpoint, e.g. <c>http://localhost:5000/bridge</c>.</summary>
    public string Url { get; set; } = "http://localhost:5000/bridge";

    /// <summary>Address the proxy should bind the per-session TCP listener to (default: all interfaces).</summary>
    public string ListenAddress { get; set; } = "0.0.0.0";

    /// <summary>TCP port the proxy should listen on for POS software (default: 9100).</summary>
    public int ListenPort { get; set; } = 9100;

    public ValueTask<bool> IsSupportedAsync() => new(true);

    public async Task ConnectAsync()
    {
        if (IsConnected)
            return;

        var hub = new HubConnectionBuilder().WithUrl(Url).WithAutomaticReconnect().Build();
        hub.On<byte[]>(nameof(IBridgeClient.ReceiveEscPos), data => _sink.Feed(data, this));
        hub.Closed += _ => { OnClosed(); return Task.CompletedTask; };

        var server = new BridgeServerProxy(hub);
        // Re-open the TCP listener after an automatic reconnect (the connection id changes).
        hub.Reconnected += async _ =>
        {
            try { await server.AttachEmulator(ListenAddress, ListenPort); } catch { /* surfaces on next use */ }
        };

        try
        {
            await hub.StartAsync();
            await server.AttachEmulator(ListenAddress, ListenPort);
        }
        catch (Exception ex)
        {
            try { await hub.DisposeAsync(); } catch { }
            Description = ex.Message; // e.g. "Could not listen on 0.0.0.0:9100 — port in use"
            StateChanged?.Invoke();
            return;
        }

        _hub = hub;
        _server = server;
        IsConnected = true;
        Description = $"{Url}  (TCP {ListenAddress}:{ListenPort})";
        _sink.Attach(this);
        StateChanged?.Invoke();
    }

    /// <summary>The printer's status reply → back through the hub to whoever sent the job.</summary>
    public void Send(byte[] data)
    {
        if (_hub is { State: HubConnectionState.Connected } && _server is not null)
            _ = _server.ReplyToSender(data);
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected)
            return;
        _sink.Detach(this);
        IsConnected = false;
        Description = null;
        var hub = _hub;
        _hub = null;
        _server = null;
        if (hub is not null)
        {
            try { await hub.DisposeAsync(); } catch { }
        }
        StateChanged?.Invoke();
    }

    private void OnClosed()
    {
        if (!IsConnected)
            return;
        _sink.Detach(this);
        IsConnected = false;
        Description = null;
        StateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
