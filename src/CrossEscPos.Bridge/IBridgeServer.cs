using System.Threading.Tasks;

namespace CrossEscPos.Bridge;

/// <summary>
/// The broker hub's server methods — what a client invokes on the server. The hub implements this, and
/// clients proxy it (see the browser-side <c>BridgeServerProxy</c>) so both ends share one contract.
/// </summary>
public interface IBridgeServer
{
    /// <summary>
    /// Register this connection as the printer (emulator) and ask the proxy to open a TCP listener on
    /// <paramref name="address"/>:<paramref name="port"/> for this session, so POS software can reach it.
    /// Throws if the port can't be bound. The listener is torn down when the emulator disconnects.
    /// </summary>
    Task AttachEmulator(string address, int port);

    /// <summary>Send an ESC/POS job (from a monitor/POS) to the attached emulator.</summary>
    Task SendToEmulator(byte[] data);

    /// <summary>The emulator's status reply, routed back to whoever last sent.</summary>
    Task ReplyToSender(byte[] data);
}
