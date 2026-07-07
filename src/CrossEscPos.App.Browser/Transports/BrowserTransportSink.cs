using CrossEscPos;
using CrossEscPos.Emulator;
using CrossEscPos.Transports.Browser;

namespace CrossEscPos.App.Browser.Transports;

/// <summary>Feeds live transport bytes into the shared printer; the MainViewModel refreshes on activity.</summary>
public sealed class BrowserTransportSink : ITransportSink
{
    private readonly ReceiptPrinter _printer;

    public BrowserTransportSink(ReceiptPrinter printer) => _printer = printer;

    public void Feed(byte[] data, IPrinterResponder responder) => _printer.FeedEscPos(data, responder);
    public void Attach(IPrinterResponder responder) => _printer.RegisterResponder(responder);
    public void Detach(IPrinterResponder responder) => _printer.UnregisterResponder(responder);
}
