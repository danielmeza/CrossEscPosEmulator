using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Logging;

namespace ReceiptPrinterEmulator.Networking;

/// <summary>
/// TCP listener that receives ESC/POS data and feeds it to the printer. Can be started, stopped and
/// re-bound at runtime (e.g. from the UI) to a different address/port.
/// </summary>
public class NetServer
{
    public IPEndPoint? EndPoint { get; private set; }
    public bool IsRunning { get; private set; }

    private readonly ReceiptPrinter _printer;
    private Socket? _tcpSocket;
    private CancellationTokenSource? _cts;
    private readonly List<NetClient> _clients = new();

    public NetServer(ReceiptPrinter printer)
    {
        _printer = printer;
    }

    /// <summary>
    /// Binds and starts listening on the given address/port. Returns true on success; on failure the
    /// server is left stopped and the error is logged. Re-binding stops any previous listener first.
    /// </summary>
    public bool Start(IPAddress address, int port)
    {
        Stop();

        try
        {
            EndPoint = new IPEndPoint(address, port);

            Logger.Info($"Starting NetServer on {EndPoint}");

            _tcpSocket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _tcpSocket.Bind(EndPoint);
            _tcpSocket.Listen(8);

            IsRunning = true;
            _cts = new CancellationTokenSource();

            _ = AcceptLoopAsync(_cts.Token);

            Logger.Info($"Server bound to {EndPoint}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, $"Failed to start TCP listener on {address}:{port}");
            Stop();
            return false;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;

        if (_tcpSocket is not null)
        {
            try { _tcpSocket.Close(); }
            catch (Exception ex) { Logger.Exception(ex, "Error closing TCP socket"); }
            _tcpSocket.Dispose();
            _tcpSocket = null;
        }

        IsRunning = false;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (IsRunning && !cancellationToken.IsCancellationRequested)
            {
                var clientSocket = await _tcpSocket!.AcceptAsync(cancellationToken);

                if (!clientSocket.Connected)
                    continue;

                var client = new NetClient(this, _printer, clientSocket);
                _clients.Add(client);

                Logger.Info($"Accepted new connection (RemoteEndPoint={client.RemoteEndPoint})");

                _ = client.ReceiveLoopAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Stop().
        }
        catch (ObjectDisposedException)
        {
            // Socket closed during Stop().
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "TCP accept loop error");
        }
    }
}
