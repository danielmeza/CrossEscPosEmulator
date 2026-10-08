using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CrossEscPos.Transports.Tests;

/// <summary>
/// A <see cref="Stream"/> standing in for a device, so the host-side transport can be exercised
/// without hardware. Writes are recorded individually (making write splitting observable) and reads
/// serve whatever the test queued on the status channel, returning 0 when nothing is pending — the
/// way a bulk read that timed out does.
/// </summary>
internal class FakeTransport : Stream
{
    private readonly Lock _gate = new();
    private readonly List<byte[]> _writes = new();
    private readonly Queue<byte> _status = new();
    private readonly bool _canRead;

    private int _flushes;
    private int _readAttempts;
    private bool _disposed;

    public FakeTransport(bool canRead = true) => _canRead = canRead;

    /// <summary>Every write the printer issued, in order and un-coalesced.</summary>
    public IReadOnlyList<byte[]> Writes { get { lock (_gate) return _writes.ToArray(); } }

    /// <summary>Everything written, concatenated back into one job.</summary>
    public byte[] WrittenBytes
    {
        get
        {
            lock (_gate)
            {
                var all = new List<byte>();
                foreach (var w in _writes)
                    all.AddRange(w);
                return all.ToArray();
            }
        }
    }

    public int Flushes { get { lock (_gate) return _flushes; } }
    public int ReadAttempts { get { lock (_gate) return _readAttempts; } }
    public bool IsDisposed { get { lock (_gate) return _disposed; } }

    /// <summary>Writes that landed after this stream was disposed — undefined behaviour for a real stream.</summary>
    public int WritesAfterDispose { get { lock (_gate) return _writesAfterDispose; } }

    private int _writesAfterDispose;

    /// <summary>Makes bytes available on the printer's status channel.</summary>
    public void PushStatus(params byte[] data)
    {
        lock (_gate)
            foreach (var b in data)
                _status.Enqueue(b);
    }

    public override bool CanRead => _canRead;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush() { lock (_gate) _flushes++; }

    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            if (_disposed)
                _writesAfterDispose++;
            _writes.Add(buffer.AsSpan(offset, count).ToArray());
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            _readAttempts++;
            if (!_canRead)
                throw new NotSupportedException("send-only transport");

            int n = 0;
            while (n < count && _status.Count > 0)
                buffer[offset + n++] = _status.Dequeue();
            return n; // 0 — nothing on the status channel yet.
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            lock (_gate)
                _disposed = true;
        base.Dispose(disposing);
    }
}
