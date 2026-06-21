using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrossEscPos.Emulator;
using CrossEscPos;
using CrossEscPos.Logging;

namespace CrossEscPos.Transports;

/// <summary>
/// Receives ESC/POS data over a serial port (RS-232 / USB-serial / virtual PTY) and feeds it to the
/// printer, mirroring <see cref="NetServer"/>. Can be opened/closed at runtime (e.g. from the UI).
/// The interpreter keeps its state across calls, so the byte stream may arrive in arbitrary chunks
/// (commands spanning reads are handled correctly).
///
/// To simulate without hardware, create a virtual serial pair:
///   macOS/Linux:  socat -d -d pty,raw,echo=0 pty,raw,echo=0
///   Windows:      com0com  (creates a linked COM pair, e.g. COM3 &lt;-&gt; COM4)
/// </summary>
public class SerialServer : IPrinterResponder
{
    public string PortName { get; private set; } = string.Empty;
    public int BaudRate { get; private set; }
    public bool IsRunning { get; private set; }

    private readonly ReceiptPrinter _printer;
    private SerialPort? _port;
    private CancellationTokenSource? _cts;

    public SerialServer(ReceiptPrinter printer)
    {
        _printer = printer;
    }

    /// <summary>
    /// Opens the given serial port. Returns true on success; on failure the server is left closed and
    /// the error is logged. Re-opening closes any previous port first.
    /// </summary>
    public bool Start(string portName, int baudRate = 9600)
    {
        Stop();

        try
        {
            PortName = portName;
            BaudRate = baudRate;

            Logger.Info($"Opening serial port {PortName} @ {BaudRate} baud");

            _port = new SerialPort(PortName, BaudRate)
            {
                ReadTimeout = SerialPort.InfiniteTimeout,
                Handshake = Handshake.None
            };
            _port.Open();
            IsRunning = true;
            _printer.RegisterResponder(this);

            _cts = new CancellationTokenSource();
            _ = ReceiveLoopAsync(_cts.Token);

            return true;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, $"Failed to open serial port {portName}");
            Stop();
            return false;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;

        _printer.UnregisterResponder(this);

        if (_port is not null)
        {
            try { if (_port.IsOpen) _port.Close(); }
            catch (Exception ex) { Logger.Exception(ex, "Error closing serial port"); }
            _port.Dispose();
            _port = null;
        }

        IsRunning = false;
    }

    /// <summary>Writes a printer response (status bytes, printer ID, …) back over the serial port.</summary>
    public void Send(byte[] data)
    {
        try
        {
            var port = _port;
            if (port is { IsOpen: true })
                port.BaseStream.Write(data, 0, data.Length);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, $"Failed to send {data.Length} bytes over {PortName}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var stream = _port!.BaseStream;
        var buffer = new byte[64 * 1024];

        try
        {
            while (IsRunning && !cancellationToken.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read <= 0)
                    continue;

                Logger.Info($"Received serial data (byteCount={read}, port={PortName})");

                _printer.FeedEscPos(buffer.AsSpan(0, read), this);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Stop().
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, $"Serial read error on {PortName}");
            IsRunning = false;
        }
    }
}
