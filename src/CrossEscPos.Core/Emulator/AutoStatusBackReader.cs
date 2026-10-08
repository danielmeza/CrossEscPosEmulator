using System;

namespace CrossEscPos.Emulator;

/// <summary>
/// What one Automatic Status Back block says about the printer, as a host reading the status channel
/// sees it — the inverse of <see cref="StatusByteBuilder.AutoStatusBack"/>.
/// </summary>
/// <remarks>
/// The ASB block carries three further flags that nothing in this repository reads
/// (<c>paper currently feeding</c>, <c>waiting for online recovery</c>, <c>feed button pushed</c>),
/// so they are not surfaced here. Add them to this record if a consumer ever needs them.
/// </remarks>
public sealed record PrinterStatusReport(
    bool Online,
    bool PaperOut,
    bool PaperLow,
    bool CoverOpen,
    bool DrawerOpen,
    bool Error);

/// <summary>
/// Reassembles and decodes the Automatic Status Back blocks a printer pushes back on its status
/// channel. A transport feeds raw bytes in as they arrive — in whatever sizes the wire delivers them
/// — and gets one complete 4-byte block out at a time.
///
/// Bit layouts are the Epson TM ASB format, read back exactly as
/// <see cref="StatusByteBuilder.AutoStatusBack"/> writes them.
/// </summary>
public sealed class AutoStatusBackReader
{
    /// <summary>Length of one ASB block, in bytes.</summary>
    public const int FrameLength = 4;

    // Byte 0 of a block has bit 4 set and bits 0, 1 and 7 clear.
    private const byte Byte0MustBeClear = 0b1000_0011;
    private const byte Byte0MustBeSet = 0b0001_0000;

    private readonly byte[] _frame = new byte[FrameLength];
    private int _filled;

    /// <summary>
    /// Feeds bytes read from the status channel, invoking <paramref name="onFrame"/> once per
    /// complete block. Bytes left over from a partial block are kept for the next call, so a block
    /// split across reads is still delivered whole.
    /// </summary>
    public void Feed(ReadOnlySpan<byte> data, Action<byte[]> onFrame)
    {
        ArgumentNullException.ThrowIfNull(onFrame);

        foreach (byte b in data)
        {
            _frame[_filled++] = b;
            if (_filled < FrameLength)
                continue;

            _filled = 0;
            // A copy: the buffer is refilled by the next block, and a handler may keep this one.
            onFrame(_frame.AsSpan().ToArray());
        }
    }

    /// <summary>
    /// Decodes one ASB block. Returns <c>null</c> when <paramref name="frame"/> is not an ASB block
    /// — the wrong length, or byte 0 without the block's fixed bit pattern (a <c>DLE EOT</c> or
    /// <c>GS r</c> reply that happens to be four bytes long, say). Rejecting those rather than
    /// decoding them keeps a status the printer never sent out of the host's hands.
    /// </summary>
    public static PrinterStatusReport? Parse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length != FrameLength)
            return null;

        byte printer = frame[0], error = frame[1], paper = frame[2];

        if ((printer & Byte0MustBeClear) != 0 || (printer & Byte0MustBeSet) == 0)
            return null;

        // Each paper sensor reading is duplicated across two bits, so both must agree before it
        // counts; and once the roll is out, near-end says nothing a caller still needs.
        bool paperOut = (paper & 0x04) != 0 && (paper & 0x08) != 0;   // bits 2 + 3
        bool paperLow = !paperOut && (paper & 0x01) != 0 && (paper & 0x02) != 0; // bits 0 + 1

        return new PrinterStatusReport(
            Online: (printer & 0x08) == 0,      // bit 3 set = offline
            PaperOut: paperOut,
            PaperLow: paperLow,
            CoverOpen: (printer & 0x20) != 0,   // bit 5
            DrawerOpen: (printer & 0x04) == 0,  // bit 2 set = drawer closed
            // Recoverable (6), unrecoverable (5), autocutter (3), recoverable non-autocutter (2).
            Error: (error & 0x6C) != 0);
    }
}
