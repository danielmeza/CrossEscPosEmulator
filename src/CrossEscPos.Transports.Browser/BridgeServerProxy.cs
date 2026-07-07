using System.Threading.Tasks;
using CrossEscPos.Bridge;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrossEscPos.Transports.Browser;

/// <summary>
/// Client-side typed proxy of the broker's <see cref="IBridgeServer"/>: each call forwards to the hub by
/// the interface method name, so the emulator and monitor clients invoke server methods type-safely
/// instead of with magic strings. The matching server→client direction is the strongly-typed hub itself.
/// </summary>
public sealed class BridgeServerProxy : IBridgeServer
{
    private readonly HubConnection _hub;

    public BridgeServerProxy(HubConnection hub) => _hub = hub;

    public Task AttachEmulator(string address, int port)
        => _hub.InvokeAsync(nameof(IBridgeServer.AttachEmulator), address, port);
    public Task SendToEmulator(byte[] data) => _hub.InvokeAsync(nameof(IBridgeServer.SendToEmulator), data);
    public Task ReplyToSender(byte[] data) => _hub.InvokeAsync(nameof(IBridgeServer.ReplyToSender), data);
}
