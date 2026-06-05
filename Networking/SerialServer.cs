using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Logging;

namespace ReceiptPrinterEmulator.Networking;

/// <summary>
/// Receives ESC/POS data over a serial port (RS-232 / USB-serial / virtual PTY) and feeds it to the
/// printer, mirroring <see cref="NetServer"/>. The interpreter keeps its state across calls, so the
/// byte stream may arrive in arbitrary chunks (commands spanning reads are handled correctly).
///
/// To simulate without hardware, create a virtual serial pair:
///   macOS/Linux:  socat -d -d pty,raw,echo=0 pty,raw,echo=0   (point the app at one PTY, write to the other)
///   Windows:      com0com  (creates a linked COM pair, e.g. COM3 &lt;-&gt; COM4)
/// </summary>
public class SerialServer
{
    public string PortName { get; }
    public int BaudRate { get; }
    public bool IsRunning { get; private set; }

    private readonly ReceiptPrinter _printer;
    private SerialPort? _port;
    private CancellationTokenSource? _cts;

    public SerialServer(ReceiptPrinter printer, string portName, int baudRate = 9600)
    {
        _printer = printer;
        PortName = portName;
        BaudRate = baudRate;
    }

    public async Task Run()
    {
        Stop();

        try
        {
            Logger.Info($"Opening serial port {PortName} @ {BaudRate} baud");

            _port = new SerialPort(PortName, BaudRate)
            {
                ReadTimeout = SerialPort.InfiniteTimeout,
                Handshake = Handshake.None
            };
            _port.Open();
            IsRunning = true;

            _cts = new CancellationTokenSource();
            await ReceiveLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            IsRunning = false;
            Logger.Exception(ex, $"Serial port error on {PortName}");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;

        if (_port is not null)
        {
            try { if (_port.IsOpen) _port.Close(); }
            catch (Exception ex) { Logger.Exception(ex, "Error closing serial port"); }
            _port.Dispose();
            _port = null;
        }

        IsRunning = false;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var stream = _port!.BaseStream;
        var buffer = new byte[64 * 1024];

        while (IsRunning && !cancellationToken.IsCancellationRequested)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
                continue;

            Logger.Info($"Received serial data (byteCount={read}, port={PortName})");

            _printer.FeedEscPos(Encoding.Latin1.GetString(buffer, 0, read));
        }
    }
}
