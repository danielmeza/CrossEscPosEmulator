using System.Net;
using System.Net.Sockets;
using CrossEscPos.Bridge;
using CrossEscPos.Host;
using Microsoft.AspNetCore.SignalR;

// CrossEscPos.Host — the single web host for the browser experience. It serves the Avalonia WASM app
// (published into wwwroot) AND hosts the SignalR broker + the TCP:9100 listener that bridges a real POS
// to the in-page emulator. Everything is one origin, so the browser reaches the hub at /bridge with no
// CORS. Browse to the host root to run the emulator; point POS software at :9100; the Monitor rides the
// same hub for a full round-trip.

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();

var app = builder.Build();

// Serve the WASM app: _framework/* with the right MIME types, then the static content, with a fallback
// to index.html for the SPA.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapHub<BridgeHub>("/bridge");
BridgeHub.Context_ = app.Services.GetRequiredService<IHubContext<BridgeHub, IBridgeClient>>();

app.MapFallbackToFile("index.html");

// Accept POS TCP clients and bridge each to the SignalR emulator.
int tcpPort = int.TryParse(Environment.GetEnvironmentVariable("PROXY_TCP_PORT"), out var tp) ? tp : 9100;
var listener = new TcpListener(IPAddress.Any, tcpPort);
listener.Start();
Console.WriteLine($"[tcp] listening on :{tcpPort} (bridging to the SignalR emulator at /bridge)");

_ = Task.Run(async () =>
{
    while (true)
    {
        var client = await listener.AcceptTcpClientAsync();
        _ = Task.Run(() => BridgeHub.BridgeTcpAsync(client));
    }
});

app.Run();
