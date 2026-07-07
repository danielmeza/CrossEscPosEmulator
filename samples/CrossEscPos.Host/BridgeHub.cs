using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CrossEscPos.Bridge;
using Microsoft.AspNetCore.SignalR;

namespace CrossEscPos.Host;

/// <summary>
/// The broker hub — strongly typed on both sides: it implements <see cref="IBridgeServer"/> (the methods
/// clients call) and is a <see cref="Hub{T}"/> of <see cref="IBridgeClient"/> (the methods it calls back,
/// e.g. <c>Clients.Client(id).ReceiveEscPos(data)</c>). When an emulator attaches it opens a TCP listener
/// on the address:port it asks for; POS software that connects there — or a SignalR monitor — sends jobs
/// to that emulator and gets its status replies back. The listener is torn down when the emulator leaves.
/// </summary>
public sealed class BridgeHub : Hub<IBridgeClient>, IBridgeServer
{
    internal static string? EmulatorConnection;
    internal static Func<byte[], Task>? SenderReply;                 // routes status to the current sender
    internal static IHubContext<BridgeHub, IBridgeClient>? Context_; // strongly-typed client proxy

    private static readonly ConcurrentDictionary<string, TcpSession> Sessions = new();
    private static readonly ConcurrentDictionary<string, TcpClient> Outbound = new(); // monitor → printer

    private sealed record TcpSession(TcpListener Listener, CancellationTokenSource Cts);

    public Task AttachEmulator(string address, int port)
    {
        var connection = Context.ConnectionId;
        EmulatorConnection = connection;
        StopSession(connection); // drop any prior listener for this connection (re-attach)

        var ip = IPAddress.TryParse(address, out var parsed) ? parsed : IPAddress.Any;
        TcpListener listener;
        try
        {
            listener = new TcpListener(ip, port);
            listener.Start();
        }
        catch (Exception ex)
        {
            throw new HubException($"Could not listen on {address}:{port} — {ex.Message}");
        }

        var cts = new CancellationTokenSource();
        Sessions[connection] = new TcpSession(listener, cts);
        Console.WriteLine($"[hub] emulator attached; listening on {address}:{port}");
        _ = AcceptLoop(connection, listener, cts.Token);
        return Task.CompletedTask;
    }

    public async Task ConnectTcp(string host, int port)
    {
        var connection = Context.ConnectionId;
        CloseOutbound(connection);

        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port);
        }
        catch (Exception ex)
        {
            client.Dispose();
            throw new HubException($"Could not connect to {host}:{port} — {ex.Message}");
        }

        Outbound[connection] = client;
        Console.WriteLine($"[tcp-out] monitor → printer {host}:{port}");
        _ = PumpOutbound(connection, client);
    }

    public Task SendToEmulator(byte[] data)
    {
        var connection = Context.ConnectionId;

        // A monitor that opened an outbound TCP printer writes to that socket instead of the emulator.
        if (Outbound.TryGetValue(connection, out var printer))
        {
            try { return printer.GetStream().WriteAsync(data).AsTask(); }
            catch { return Task.CompletedTask; }
        }

        SenderReply = d => Context_!.Clients.Client(connection).ReceiveStatus(d);
        return ForwardToEmulator(data);
    }

    public Task ReplyToSender(byte[] data) => SenderReply?.Invoke(data) ?? Task.CompletedTask;

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var connection = Context.ConnectionId;
        StopSession(connection);
        CloseOutbound(connection);
        if (EmulatorConnection == connection)
            EmulatorConnection = null;
        return base.OnDisconnectedAsync(exception);
    }

    private static void CloseOutbound(string connection)
    {
        if (Outbound.TryRemove(connection, out var client))
        {
            try { client.Close(); client.Dispose(); } catch { /* already gone */ }
            Console.WriteLine("[tcp-out] printer connection closed");
        }
    }

    /// <summary>Pump a monitor's outbound TCP printer: its bytes → the monitor as status replies.</summary>
    private static async Task PumpOutbound(string connection, TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                if (Context_ is not null)
                    await Context_.Clients.Client(connection).ReceiveStatus(buffer[..read]);
            }
        }
        catch { /* printer gone */ }
        CloseOutbound(connection);
    }

    private static Task ForwardToEmulator(byte[] data)
        => EmulatorConnection is not null && Context_ is not null
            ? Context_.Clients.Client(EmulatorConnection).ReceiveEscPos(data)
            : Task.CompletedTask;

    private static void StopSession(string connection)
    {
        if (Sessions.TryRemove(connection, out var session))
        {
            try { session.Cts.Cancel(); session.Listener.Stop(); } catch { /* already gone */ }
            Console.WriteLine("[hub] TCP session stopped");
        }
    }

    /// <summary>Accept POS TCP clients for one emulator session until the listener is stopped.</summary>
    private static async Task AcceptLoop(string emulatorConnection, TcpListener listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = BridgeTcpAsync(emulatorConnection, client, ct);
            }
        }
        catch { /* listener stopped */ }
    }

    /// <summary>Bridge a raw TCP POS client: its bytes → its emulator; the emulator's status → its stream.</summary>
    private static async Task BridgeTcpAsync(string emulatorConnection, TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var stream = client.GetStream();
            SenderReply = d => stream.WriteAsync(d, CancellationToken.None).AsTask();
            Console.WriteLine("[tcp] POS connected — bridging to emulator");
            var buffer = new byte[4096];
            int read;
            try
            {
                while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    if (Context_ is not null)
                        await Context_.Clients.Client(emulatorConnection).ReceiveEscPos(buffer[..read]);
                }
            }
            catch { /* POS gone / cancelled */ }
            Console.WriteLine("[tcp] POS disconnected");
        }
    }
}
