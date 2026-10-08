using System;
using System.IO;
using System.Threading;
using CrossEscPos.Emulator;

namespace CrossEscPos.Transports;

/// <summary>
/// A printer driven over a byte stream: ESC/POS jobs go out through <see cref="Send"/>, and the
/// Automatic Status Back blocks the printer pushes back arrive on <see cref="StatusFrameReceived"/>.
///
/// This is the host side of the conversation — the mirror of <see cref="NetServer"/> /
/// <see cref="SerialServer"/>, which play the printer. It carries no ESC/POS knowledge beyond
/// handing the status channel to <see cref="AutoStatusBackReader"/>; subclasses supply the stream.
/// </summary>
public class StreamPrinter : IPrinterResponder, IDisposable
{
    /// <summary>
    /// Largest single write handed to the stream; a longer job is split into this many bytes at a
    /// time. Receipt printers have small input buffers and some transports cap a transfer's size.
    /// </summary>
    public const int MaxBytesPerWrite = 15_000;

    private const int ReadBufferSize = 64;   // one full-speed bulk packet; an ASB block is 4 bytes.
    private const int IdleReadDelayMs = 50;  // back-off after a read that returned nothing.
    private const int StopTimeoutMs = 2_000; // bound on waiting for the status thread to finish.

    private readonly Stream _stream;
    private readonly AutoStatusBackReader _statusReader = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _writeLock = new();
    private readonly Thread? _statusThread;

    private volatile bool _disposed;

    /// <summary>A label for logs and errors (the device or endpoint this talks to).</summary>
    public string Name { get; }

    /// <summary>
    /// Raised once per complete 4-byte Automatic Status Back block read from the printer. Raised on
    /// the status thread, so a handler that touches UI state must marshal to its own thread.
    /// Decode a block with <see cref="AutoStatusBackReader.Parse"/>.
    /// </summary>
    public event Action<byte[]>? StatusFrameReceived;

    /// <param name="stream">
    /// The transport. Writes are the ESC/POS channel; reads are the printer's status channel. A
    /// stream whose <see cref="Stream.CanRead"/> is false is treated as send-only and no status
    /// thread is started. Disposed with this instance.
    /// </param>
    /// <param name="name">A label for this printer; see <see cref="Name"/>.</param>
    public StreamPrinter(Stream stream, string name)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        Name = string.IsNullOrEmpty(name) ? stream.GetType().Name : name;

        if (!_stream.CanRead)
            return;

        _statusThread = new Thread(ReadStatusLoop) { IsBackground = true, Name = $"{Name} status" };
        _statusThread.Start();
    }

    /// <summary>
    /// Writes an ESC/POS job to the printer, split into <see cref="MaxBytesPerWrite"/>-byte writes
    /// and flushed once the whole job is out. Calls are serialised, so two senders cannot interleave
    /// halves of their jobs on the wire.
    /// </summary>
    /// <exception cref="ObjectDisposedException">This printer has been disposed.</exception>
    public void Send(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (data.Length == 0)
            return;

        lock (_writeLock)
        {
            for (int sent = 0; sent < data.Length;)
            {
                int chunk = Math.Min(MaxBytesPerWrite, data.Length - sent);
                _stream.Write(data, sent, chunk);
                sent += chunk;
            }
            _stream.Flush();
        }
    }

    private void ReadStatusLoop()
    {
        var buffer = new byte[ReadBufferSize];
        var token = _stopping.Token;

        while (!token.IsCancellationRequested)
        {
            int read;
            try
            {
                read = _stream.Read(buffer, 0, buffer.Length);
            }
            catch (ObjectDisposedException)
            {
                return; // the stream is gone for good — there is nothing left to retry.
            }
            catch (Exception)
            {
                // A failed status read must not take printing down with it, and must not turn into a
                // hot loop: treat it as "nothing arrived", back off below, and try again. A hard
                // transport failure still surfaces to the caller through Send.
                read = 0;
            }

            if (read <= 0)
            {
                // A zero-length read means "no status yet", not end of stream: the status channel
                // stays open for the printer's whole life and is idle most of the time.
                if (!token.IsCancellationRequested)
                    Thread.Sleep(IdleReadDelayMs);
                continue;
            }

            _statusReader.Feed(buffer.AsSpan(0, read), frame => StatusFrameReceived?.Invoke(frame));
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;

        if (!disposing)
            return;

        // Stop the status thread and wait for it before the stream goes away: letting a read sit in
        // flight while the transport is torn down is a native use-after-free on USB, which no
        // managed catch can recover from.
        try { _stopping.Cancel(); } catch { /* ignore */ }
        try { _statusThread?.Join(StopTimeoutMs); } catch { /* ignore */ }
        try { _stream.Dispose(); } catch { /* ignore */ }
        try { _stopping.Dispose(); } catch { /* ignore */ }
    }
}
