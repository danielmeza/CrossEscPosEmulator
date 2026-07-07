using CrossEscPos.Bridge;
using CrossEscPos.Host;
using Microsoft.AspNetCore.SignalR;

// CrossEscPos.Host — the single web host for the browser experience. It serves the Avalonia WASM app
// (published into wwwroot) AND hosts the SignalR broker on one origin, so the browser reaches the hub at
// /bridge with no CORS. The browser emulator asks the hub to open a TCP listener on the address:port it
// chooses (per session); POS software connects there and its jobs bridge to the in-page emulator, and the
// Monitor rides the same hub for a full round-trip.

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

app.Run();
