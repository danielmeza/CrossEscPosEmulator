using System.Threading.Tasks;

namespace CrossEscPos.Bridge;

/// <summary>
/// The broker hub's server methods — what a client invokes on the server. The hub implements this, and
/// clients proxy it (see the browser-side <c>BridgeServerProxy</c>) so both ends share one contract.
/// </summary>
public interface IBridgeServer
{
    /// <summary>Register this connection as the printer (emulator) the proxy forwards jobs to.</summary>
    Task AttachEmulator();

    /// <summary>Send an ESC/POS job (from a monitor/POS) to the attached emulator.</summary>
    Task SendToEmulator(byte[] data);

    /// <summary>The emulator's status reply, routed back to whoever last sent.</summary>
    Task ReplyToSender(byte[] data);
}
