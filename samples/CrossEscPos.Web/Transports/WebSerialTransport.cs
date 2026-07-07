using CrossEscPos.Web.Services;
using Microsoft.JSInterop;

namespace CrossEscPos.Web.Transports;

/// <summary>
/// Feeds ESC/POS from a serial port via the <b>Web Serial API</b> (Chromium, HTTPS/localhost). Reads the
/// port's readable stream into <c>FeedEscPos</c> and writes status replies to the writable stream — the
/// browser analogue of the desktop <c>SerialServer</c>.
/// </summary>
public sealed class WebSerialTransport : WebTransportBase
{
    public WebSerialTransport(EmulatorHost host, IJSRuntime js) : base(host, js) { }

    protected override string JsObject => "crossescposSerial";
    public override string Kind => "Web Serial";
}
