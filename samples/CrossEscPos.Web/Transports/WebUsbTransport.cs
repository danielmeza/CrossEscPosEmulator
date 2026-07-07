using CrossEscPos.Web.Services;
using Microsoft.JSInterop;

namespace CrossEscPos.Web.Transports;

/// <summary>
/// Feeds ESC/POS from a USB device via the <b>WebUSB API</b> (Chromium, HTTPS/localhost). The browser is
/// the USB host: it reads the device's bulk <c>IN</c> endpoint into <c>FeedEscPos</c> and writes status
/// replies to the bulk <c>OUT</c> endpoint. Suits devices/adapters that stream ESC/POS to the host.
/// </summary>
public sealed class WebUsbTransport : WebTransportBase
{
    public WebUsbTransport(EmulatorHost host, IJSRuntime js) : base(host, js) { }

    protected override string JsObject => "crossescposUsb";
    public override string Kind => "WebUSB";
}
