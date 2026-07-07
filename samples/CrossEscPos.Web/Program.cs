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

await builder.Build().RunAsync();
