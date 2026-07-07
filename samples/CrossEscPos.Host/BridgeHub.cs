using System.Net.Sockets;
using CrossEscPos.Bridge;
using Microsoft.AspNetCore.SignalR;

namespace CrossEscPos.Host;

/// <summary>
/// The broker hub — strongly typed on both sides: it implements <see cref="IBridgeServer"/> (the methods
/// clients call) and is a <see cref="Hub{T}"/> of <see cref="IBridgeClient"/> (the methods it calls back,
/// e.g. <c>Clients.Client(id).ReceiveEscPos(data)</c>). One emulator attaches; senders — a SignalR monitor
/// or a TCP:9100 POS client bridged in — send jobs to it and get its status replies back (last-sender-wins).
/// </summary>
public sealed class BridgeHub : Hub<IBridgeClient>, IBridgeServer
{
    internal static string? EmulatorConnection;
    internal static Func<byte[], Task>? SenderReply;                 // routes status to the current sender
    internal static IHubContext<BridgeHub, IBridgeClient>? Context_; // strongly-typed client proxy

    public Task AttachEmulator()
    {
        EmulatorConnection = Context.ConnectionId;
        Console.WriteLine("[hub] emulator attached");
        return Task.CompletedTask;
    }

    public Task SendToEmulator(byte[] data)
    {
        var connection = Context.ConnectionId;
        SenderReply = d => Context_!.Clients.Client(connection).ReceiveStatus(d);
        return ForwardToEmulator(data);
    }

    public Task ReplyToSender(byte[] data) => SenderReply?.Invoke(data) ?? Task.CompletedTask;

    internal static Task ForwardToEmulator(byte[] data)
        => EmulatorConnection is not null && Context_ is not null
            ? Context_.Clients.Client(EmulatorConnection).ReceiveEscPos(data)
            : Task.CompletedTask;

    /// <summary>Bridge a raw TCP:9100 POS client: its bytes → emulator; the emulator's status → its stream.</summary>
    internal static async Task BridgeTcpAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            SenderReply = d => stream.WriteAsync(d).AsTask();
            Console.WriteLine("[tcp] POS connected — bridging to emulator");
            var buffer = new byte[4096];
            int read;
            try
            {
                while ((read = await stream.ReadAsync(buffer)) > 0)
                    await ForwardToEmulator(buffer[..read]);
            }
            catch { /* POS gone */ }
            Console.WriteLine("[tcp] POS disconnected");
        }
    }
}
