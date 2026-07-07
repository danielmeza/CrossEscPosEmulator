using CrossEscPos;   // IPrinterResponder

namespace CrossEscPos.Transports.Browser;

/// <summary>
/// The host's hook for a live transport: where received ESC/POS is fed and how a transport is
/// registered as a responder. The Blazor host's <c>EmulatorHost</c> and the Avalonia host both
/// implement this, so <see cref="WebTransport"/> stays host-agnostic.
/// </summary>
public interface ITransportSink
{
    /// <summary>Feed a chunk of received ESC/POS into the current printer (append — no reset).</summary>
    void Feed(byte[] data, IPrinterResponder responder);

    /// <summary>Register the transport as a long-lived responder (status / Automatic Status Back).</summary>
    void Attach(IPrinterResponder responder);

    /// <summary>Unregister the transport responder.</summary>
    void Detach(IPrinterResponder responder);
}
