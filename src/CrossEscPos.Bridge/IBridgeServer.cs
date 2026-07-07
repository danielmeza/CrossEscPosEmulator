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

    /// <summary>
    /// As a <b>monitor</b>, ask the proxy to open an outbound TCP connection to a real network printer at
    /// <paramref name="host"/>:<paramref name="port"/>. After this, <see cref="SendToEmulator"/> writes to
    /// that socket and its replies come back via <c>ReceiveStatus</c>. Throws if the printer can't be reached.
    /// </summary>
    Task ConnectTcp(string host, int port);

    /// <summary>
    /// Send an ESC/POS job to this connection's target — the attached emulator, or the TCP printer opened
    /// with <see cref="ConnectTcp"/> if one is active.
    /// </summary>
    Task SendToEmulator(byte[] data);

    /// <summary>The emulator's status reply, routed back to whoever last sent.</summary>
    Task ReplyToSender(byte[] data);
}
