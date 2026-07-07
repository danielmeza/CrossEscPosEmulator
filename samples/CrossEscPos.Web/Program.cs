using CrossEscPos.Web;
using CrossEscPos.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// The emulator host owns the ReceiptPrinter + the active render backend for the whole app session.
builder.Services.AddSingleton<EmulatorHost>();

// Browser transports (Web Serial / WebUSB) — one instance each, holding the live connection.
builder.Services.AddSingleton<CrossEscPos.Web.Transports.WebSerialTransport>();
builder.Services.AddSingleton<CrossEscPos.Web.Transports.WebUsbTransport>();

await builder.Build().RunAsync();
