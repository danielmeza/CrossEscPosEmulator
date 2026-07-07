using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace CrossEscPos.Transports.Browser;

/// <summary>
/// Receives ESC/POS over a <b>WebSocket</b> (via <see cref="ClientWebSocket"/>, which works on desktop
/// and in WASM alike — no JS interop). Pointed at the <c>CrossEscPos.WsProxy</c>, this gives a browser
/// TCP-style reception the sandbox otherwise forbids: the proxy listens on TCP:9100 and bridges each POS
/// connection to this socket. Frames in are fed to the printer; the printer's status replies go back out.
/// </summary>
public sealed class WebSocketTransport : IReceiptTransport
{
    private readonly ITransportSink _sink;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;

    public WebSocketTransport(ITransportSink sink, string kind)
    {
        _sink = sink;
        Kind = kind;
    }

    public string Kind { get; }
    public string? Description { get; private set; }
    public bool IsConnected { get; private set; }
    public event Action? StateChanged;

    /// <summary>The proxy endpoint to connect to, e.g. <c>ws://localhost:5000/ws</c>.</summary>
    public string Url { get; set; } = "ws://localhost:5000/ws";

    // ClientWebSocket is available on every platform we target (desktop + browser WASM).
    public ValueTask<bool> IsSupportedAsync() => new(true);

    public async Task ConnectAsync()
    {
        if (IsConnected)
            return;

        var ws = new ClientWebSocket();
        var cts = new CancellationTokenSource();
        try
        {
            await ws.ConnectAsync(new Uri(Url), cts.Token);
        }
        catch
        {
            ws.Dispose();
            return; // couldn't reach the proxy
        }

        _ws = ws;
        _cts = cts;
        IsConnected = true;
        Description = Url;
        _sink.Attach(this);
        StateChanged?.Invoke();
        _ = ReadLoopAsync(ws, cts.Token);
    }

    private async Task ReadLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                var data = message.ToArray();
                if (data.Length > 0)
                    _sink.Feed(data, this);
            }
        }
        catch { /* closed / errored */ }
        finally { OnClosed(); }
    }

    public void Send(byte[] data)
    {
        var ws = _ws;
        if (ws is { State: WebSocketState.Open })
            _ = ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected)
            return;
        _sink.Detach(this);
        IsConnected = false;
        Description = null;
        await CloseAsync();
        StateChanged?.Invoke();
    }

    private async Task CloseAsync()
    {
        try { _cts?.Cancel(); } catch { }
        var ws = _ws;
        _ws = null;
        if (ws is not null)
        {
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None); }
            catch { }
            ws.Dispose();
        }
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
