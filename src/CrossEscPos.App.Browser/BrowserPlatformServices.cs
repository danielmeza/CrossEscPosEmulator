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
/// export via Avalonia's storage provider (a download), no-op notifications, and the Web Serial /
/// WebUSB / WebSocket transports. Injected into the shared <see cref="CrossEscPos.App.App"/>, which
/// runs as a single view here (no window, no Monitor).
/// </summary>
public sealed class BrowserPlatformServices : IPlatformServices
{
    private readonly RenderBackend _backend = RenderBackend.Select(Array.Empty<string>()); // Skia
    private readonly FileDialogService _dialogs = new();
    private readonly BrowserNotificationService _notifications = new();

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
        var bridge = new WasmJsTransportBridge();
        var serial = new WebTransport(bridge, sink, "serial", "Web Serial");
        var usb = new WebTransport(bridge, sink, "usb", "WebUSB");
        var signalr = new SignalRTransport(sink, "SignalR");

        // Default the proxy to the same origin that served the app (the CrossEscPos.Host).
        var origin = WasmJsTransportBridge.PageOrigin();
        var bridgeUrl = (string.IsNullOrEmpty(origin) ? "http://localhost:5000" : origin) + "/bridge";
        signalr.Url = bridgeUrl;

        var baud = new TransportField("Baud", "9600");
        var url = new TransportField("Proxy URL", bridgeUrl);

        return new TransportEntry[]
        {
            new ReceiptTransportEntry(serial, "Web Serial", baud, v => serial.Options = v),
            new ReceiptTransportEntry(usb, "WebUSB"),
            new ReceiptTransportEntry(signalr, "TCP proxy (SignalR)", url, v => signalr.Url = v),
        };
    }

    public IMonitorClient CreateMonitorClient() => new SignalRMonitorClient();

    public bool MonitorInWindow => false; // the browser hosts the Monitor as an in-page overlay

    public void ShowMonitorWindow(MonitorViewModel monitor) { /* unused; browser hosts in-page */ }

    public void AttachRoot(Control mainView) => _dialogs.AttachControl(mainView);

    public Window CreateMainWindow(Control content)
        => throw new NotSupportedException("The browser head runs as a single view.");

    public void Shutdown() { }
}
