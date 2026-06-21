namespace CrossEscPos;

/// <summary>
/// A channel the printer can write bytes back to the host over (TCP client or serial port).
/// Implemented by the transports so status / transmit-back commands can reply to the host that
/// sent the request.
/// </summary>
public interface IPrinterResponder
{
    void Send(byte[] data);
}
