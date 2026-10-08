using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Enums;
using Xunit;

namespace CrossEscPos.Transports.Tests;

/// <summary>
/// The host side of a printer conversation over a stream: job splitting on the way out, and
/// Automatic Status Back framing on the way back. Driven over <see cref="FakeTransport"/>, so every
/// behaviour here is covered without a device attached.
///
/// This is the behaviour <c>UsbPrinter</c> used to inherit from ESC-POS-.NET's <c>BasePrinter</c>.
/// </summary>
public class StreamPrinterTests
{
    /// <summary>Generous: the status thread backs off between reads, so a frame takes a moment.</summary>
    private const int FrameWaitMs = 5_000;

    private static (StreamPrinter printer, FakeTransport transport, BlockingCollection<byte[]> frames) Connect()
    {
        var transport = new FakeTransport();
        var printer = new StreamPrinter(transport, "fake");
        var frames = new BlockingCollection<byte[]>();
        printer.StatusFrameReceived += frames.Add;
        return (printer, transport, frames);
    }

    #region Sending

    [Fact]
    public void Send_SplitsAJobLongerThanOneWrite()
    {
        var transport = new FakeTransport();
        using var printer = new StreamPrinter(transport, "fake");

        var job = new byte[StreamPrinter.MaxBytesPerWrite * 2 + 1_234];
        Random.Shared.NextBytes(job);

        printer.Send(job);

        Assert.Equal(
            new[] { StreamPrinter.MaxBytesPerWrite, StreamPrinter.MaxBytesPerWrite, 1_234 },
            transport.Writes.Select(w => w.Length).ToArray());
        Assert.Equal(job, transport.WrittenBytes);
        Assert.Equal(1, transport.Flushes); // one flush per job, not per chunk
    }

    [Fact]
    public void Send_WritesAShortJobWhole()
    {
        var transport = new FakeTransport();
        using var printer = new StreamPrinter(transport, "fake");

        var job = new byte[] { 0x1B, 0x40, 0x0A }; // ESC @ then LF
        printer.Send(job);

        Assert.Equal(job, Assert.Single(transport.Writes));
        Assert.Equal(1, transport.Flushes);
    }

    [Fact]
    public void Send_WritesNothingForAnEmptyJob()
    {
        var transport = new FakeTransport();
        using var printer = new StreamPrinter(transport, "fake");

        printer.Send(Array.Empty<byte>());

        Assert.Empty(transport.Writes);
        Assert.Equal(0, transport.Flushes);
    }

    [Fact]
    public void Send_AfterDispose_Throws()
    {
        var printer = new StreamPrinter(new FakeTransport(), "fake");
        printer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => printer.Send(new byte[] { 0x0A }));
    }

    #endregion

    #region Status channel

    [Fact]
    public void StatusChannel_RaisesOneFramePerFourByteBlock()
    {
        var (printer, transport, frames) = Connect();
        using (printer)
        {
            transport.PushStatus(0x10, 0x00, 0x00, 0x00, 0x14, 0x00, 0x03, 0x00);

            Assert.True(frames.TryTake(out var first, FrameWaitMs), "no first frame");
            Assert.True(frames.TryTake(out var second, FrameWaitMs), "no second frame");
            Assert.Equal(new byte[] { 0x10, 0x00, 0x00, 0x00 }, first);
            Assert.Equal(new byte[] { 0x14, 0x00, 0x03, 0x00 }, second);
        }
    }

    [Fact]
    public void StatusChannel_ReassemblesABlockSplitAcrossReads()
    {
        var (printer, transport, frames) = Connect();
        using (printer)
        {
            // Three bytes is not a block: nothing may be raised until the fourth arrives.
            transport.PushStatus(0x10, 0x01, 0x02);
            Assert.False(frames.TryTake(out _, 500), "a partial block was raised");

            transport.PushStatus(0x03);
            Assert.True(frames.TryTake(out var frame, FrameWaitMs), "no frame after completion");
            Assert.Equal(new byte[] { 0x10, 0x01, 0x02, 0x03 }, frame);
        }
    }

    [Fact]
    public void StatusChannel_DecodesTheEmulatorsOwnBlock()
    {
        // End to end: the emulator writes the block, the transport frames it off the wire, and the
        // reader decodes it — the whole path that used to run through BasePrinter.
        var state = new PrinterState
        {
            Online = false,
            CoverOpen = true,
            Paper = PaperLevel.Out,
            DrawerOpen = true,
            Error = PrinterErrorState.Recoverable,
        };

        var (printer, transport, frames) = Connect();
        using (printer)
        {
            transport.PushStatus(StatusByteBuilder.AutoStatusBack(state));

            Assert.True(frames.TryTake(out var frame, FrameWaitMs), "no status frame");
            var report = AutoStatusBackReader.Parse(frame);

            Assert.NotNull(report);
            Assert.False(report.Online);
            Assert.True(report.CoverOpen);
            Assert.True(report.PaperOut);
            Assert.False(report.PaperLow); // paper out wins over near-end
            Assert.True(report.DrawerOpen);
            Assert.True(report.Error);
        }
    }

    [Fact]
    public void StatusChannel_KeepsReadingAfterAFailedRead()
    {
        // A status read that throws must not end status reporting; the next block still arrives.
        var transport = new ThrowOnceTransport();
        using var printer = new StreamPrinter(transport, "fake");
        var frames = new BlockingCollection<byte[]>();
        printer.StatusFrameReceived += frames.Add;

        transport.PushStatus(0x10, 0x00, 0x00, 0x00);

        Assert.True(frames.TryTake(out var frame, FrameWaitMs), "no frame after a failed read");
        Assert.Equal(new byte[] { 0x10, 0x00, 0x00, 0x00 }, frame);
        Assert.True(transport.Threw, "the transport never got to throw");
    }

    [Fact]
    public void SendOnlyTransport_IsNeverReadFrom()
    {
        var transport = new FakeTransport(canRead: false);
        using var printer = new StreamPrinter(transport, "fake");

        printer.Send(new byte[] { 0x0A });
        Thread.Sleep(300); // long enough for a status thread to have tried several reads

        Assert.Equal(0, transport.ReadAttempts);
        Assert.Single(transport.Writes);
    }

    #endregion

    #region Lifetime

    [Fact]
    public void Dispose_DisposesTheStream()
    {
        var transport = new FakeTransport();
        var printer = new StreamPrinter(transport, "fake");

        printer.Dispose();

        Assert.True(transport.IsDisposed);
    }

    [Fact]
    public void Dispose_StopsReadingTheStatusChannel()
    {
        var transport = new FakeTransport();
        var printer = new StreamPrinter(transport, "fake");

        Thread.Sleep(200); // let the status thread get going
        printer.Dispose();

        int after = transport.ReadAttempts;
        Thread.Sleep(300);

        Assert.Equal(after, transport.ReadAttempts);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var printer = new StreamPrinter(new FakeTransport(), "fake");

        printer.Dispose();
        printer.Dispose();
    }

    [Fact]
    public void Constructor_RejectsANullStream()
        => Assert.Throws<ArgumentNullException>(() => new StreamPrinter(null!, "fake"));

    #endregion

    /// <summary>A transport whose first status read throws, to prove the read loop survives it.</summary>
    private sealed class ThrowOnceTransport : FakeTransport
    {
        private int _reads;

        public bool Threw { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Interlocked.Increment(ref _reads) == 1)
            {
                Threw = true;
                throw new InvalidOperationException("transient read failure");
            }
            return base.Read(buffer, offset, count);
        }
    }
}
