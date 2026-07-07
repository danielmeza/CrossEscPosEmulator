using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

// CrossEscPos.WsProxy — a tiny WebSocket ⟷ TCP bridge so the browser emulator can receive TCP ESC/POS
// (browsers can't open raw sockets). It LISTENS on TCP:9100 like a real network printer; the browser
// app connects to /ws and acts as the printer. POS bytes flow TCP → WS → emulator; the emulator's
// status replies flow WS → TCP → POS. One POS connection at a time (typical for print jobs).

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseWebSockets();

WebSocket? emulator = null;   // the connected browser emulator
Stream? currentPos = null;    // the POS client currently being bridged

int tcpPort = int.TryParse(Environment.GetEnvironmentVariable("PROXY_TCP_PORT"), out var tp) ? tp : 9100;

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    emulator = ws;
    Console.WriteLine("[ws] emulator connected");

    // Pump emulator → POS (status replies).
    var buffer = new byte[4096];
    try
    {
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, context.RequestAborted);
            if (result.MessageType == WebSocketMessageType.Close)
                break;
            var pos = currentPos;
            if (pos is not null && result.Count > 0)
            {
                try { await pos.WriteAsync(buffer.AsMemory(0, result.Count)); }
                catch { /* POS gone */ }
            }
        }
    }
    catch { /* socket errored */ }
    finally
    {
        if (ReferenceEquals(emulator, ws))
            emulator = null;
        Console.WriteLine("[ws] emulator disconnected");
    }
});

// Accept POS TCP clients and bridge each to the emulator WebSocket.
var listener = new TcpListener(IPAddress.Any, tcpPort);
listener.Start();
Console.WriteLine($"[tcp] listening on :{tcpPort} (bridging to the WebSocket emulator at /ws)");

_ = Task.Run(async () =>
{
    while (true)
    {
        var client = await listener.AcceptTcpClientAsync();
        _ = Task.Run(() => BridgeAsync(client));
    }
});

async Task BridgeAsync(TcpClient client)
{
    using (client)
    {
        var ws = emulator;
        if (ws is null || ws.State != WebSocketState.Open)
        {
            Console.WriteLine("[tcp] POS connected but no emulator attached — dropping");
            return;
        }

        var stream = client.GetStream();
        currentPos = stream;
        Console.WriteLine("[tcp] POS connected — bridging to emulator");

        var buffer = new byte[4096];
        int read;
        try
        {
            while ((read = await stream.ReadAsync(buffer)) > 0)
                await ws.SendAsync(new ArraySegment<byte>(buffer, 0, read), WebSocketMessageType.Binary, true, CancellationToken.None);
        }
        catch { /* POS or WS gone */ }
        finally
        {
            if (ReferenceEquals(currentPos, stream))
                currentPos = null;
            Console.WriteLine("[tcp] POS disconnected");
        }
    }
}

app.Run();
