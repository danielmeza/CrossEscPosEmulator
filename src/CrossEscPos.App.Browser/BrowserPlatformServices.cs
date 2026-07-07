using System;
using System.Collections.Generic;
using Avalonia.Controls;
using CrossEscPos.App;
using CrossEscPos.App.Browser.Monitor;
using CrossEscPos.App.Browser.Transports;
using CrossEscPos.App.Monitor;
using CrossEscPos.App.Transports;
using CrossEscPos.Controls.Services;
using CrossEscPos.Emulator;
using CrossEscPos.Graphics;
using CrossEscPos.Transports.Browser;

namespace CrossEscPos.App.Browser;

/// <summary>
/// The browser platform: the Skia render backend (Avalonia's browser renderer already links it),
/// export via Avalonia's storage provider (a download), no-op notifications, the Web Serial / WebUSB /
/// SignalR transports, and the in-page Monitor. Injected into the shared <see cref="CrossEscPos.App.App"/>,
/// which runs as a single view here.
/// </summary>
public sealed class BrowserPlatformServices : IPlatformServices
{
    private readonly RenderBackend _backend = RenderBackend.Select(Array.Empty<string>()); // Skia
    private readonly BrowserFileDialogService _dialogs = new();
    private readonly BrowserNotificationService _notifications = new();
    // One bridge instance (its [JSExport] delivery slot is static) shared by the transports + Monitor.
    private readonly WasmJsTransportBridge _bridge = new();

    public IReceiptImageFactory ImageFactory => _backend.ImageFactory;
    public ITypefaceProvider Typefaces => _backend.Typefaces;
    public IImageEncoder Encoder => _backend.Encoder;
    public string BackendName => _backend.Name;
    public IFileDialogService FileDialogs => _dialogs;
    public INotificationService Notifications => _notifications;
    public byte[] SampleTicket => Sample.Ticket;

    public IReadOnlyList<TransportEntry> CreateTransports(ReceiptPrinter printer)
    {
        var sink = new BrowserTransportSink(printer);
        var serial = new WebTransport(_bridge, sink, "serial", "Web Serial");
        var usb = new WebTransport(_bridge, sink, "usb", "WebUSB");
        var signalr = new SignalRTransport(sink, "SignalR");

        // Default the proxy to the same origin that served the app (the CrossEscPos.Host).
        var origin = WasmJsTransportBridge.PageOrigin();
        var bridgeUrl = (string.IsNullOrEmpty(origin) ? "http://localhost:5000" : origin) + "/bridge";
        signalr.Url = bridgeUrl;

        var baud = new TransportField("Baud", "9600");
        var proxyUrl = new TransportField("Proxy URL", bridgeUrl);
        var listenAddress = new TransportField("Listen address", "0.0.0.0");
        var listenPort = new TransportField("Listen port", "9100");

        return new TransportEntry[]
        {
            new ReceiptTransportEntry(serial, "Web Serial", new[] { baud }, () => serial.Options = baud.Value),
            new ReceiptTransportEntry(usb, "WebUSB"),
            new ReceiptTransportEntry(signalr, "TCP proxy (SignalR)",
                new[] { proxyUrl, listenAddress, listenPort },
                () =>
                {
                    signalr.Url = proxyUrl.Value;
                    signalr.ListenAddress = listenAddress.Value;
                    signalr.ListenPort = int.TryParse(listenPort.Value, out var p) ? p : 9100;
                },
                autoConnect: true), // comes up on load, like the desktop TCP listener
        };
    }

    public IMonitorClient CreateMonitorClient() => new WebMonitorClient(_bridge);

    public bool MonitorInWindow => false; // the browser hosts the Monitor as an in-page overlay

    public void ShowMonitorWindow(MonitorViewModel monitor) { /* unused; browser hosts in-page */ }

    public void AttachRoot(Control mainView) { /* browser downloads via JS — no top level needed */ }

    public Window CreateMainWindow(Control content)
        => throw new NotSupportedException("The browser head runs as a single view.");

    public void Shutdown() { }
}
