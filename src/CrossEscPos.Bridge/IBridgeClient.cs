using System.Threading.Tasks;

namespace CrossEscPos.Bridge;

/// <summary>
/// The methods the server invokes on a connected client. The hub is <c>Hub&lt;IBridgeClient&gt;</c>, so the
/// server calls these in a strongly-typed way (<c>Clients.Client(id).ReceiveEscPos(...)</c>); clients
/// register handlers against the same method names.
/// </summary>
public interface IBridgeClient
{
    /// <summary>Deliver an ESC/POS job to the emulator.</summary>
    Task ReceiveEscPos(byte[] data);

    /// <summary>Deliver the emulator's status reply to the sender (monitor/POS).</summary>
    Task ReceiveStatus(byte[] data);
}
