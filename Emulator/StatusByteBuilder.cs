using ReceiptPrinterEmulator.Emulator.Enums;

namespace ReceiptPrinterEmulator.Emulator;

/// <summary>
/// Builds ESC/POS status bytes from <see cref="PrinterState"/>. Bit layouts follow the Epson
/// TM-series reference (DLE EOT n, GS r n, and the 4-byte Automatic Status Back block).
/// Bits 1 and 4 are fixed to 1 in the DLE EOT / GS r single-byte responses.
/// </summary>
public static class StatusByteBuilder
{
    private const byte Fixed = 0b0001_0010; // bit1 + bit4

    /// <summary>DLE EOT 1 — printer status.</summary>
    public static byte PrinterStatus(PrinterState s)
    {
        byte b = Fixed;
        if (s.DrawerOpen) b |= 0x04;   // bit2: drawer kick-out connector pin 3
        if (!s.Online) b |= 0x08;      // bit3: 0 = online, 1 = offline
        return b;
    }

    /// <summary>DLE EOT 2 — offline cause.</summary>
    public static byte OfflineStatus(PrinterState s)
    {
        byte b = Fixed;
        if (s.CoverOpen) b |= 0x04;                    // bit2: cover open
        if (s.FeedButtonPressed) b |= 0x08;            // bit3: paper fed by feed button
        if (s.Paper == PaperLevel.Out) b |= 0x20;      // bit5: printing stopped (paper end)
        if (s.Error != PrinterErrorState.None) b |= 0x40; // bit6: error occurred
        return b;
    }

    /// <summary>DLE EOT 3 — error cause.</summary>
    public static byte ErrorStatus(PrinterState s)
    {
        byte b = Fixed;
        if (s.Error == PrinterErrorState.Unrecoverable) b |= 0x20; // bit5
        if (s.Error == PrinterErrorState.Recoverable) b |= 0x40;   // bit6: auto-recoverable
        return b;
    }

    /// <summary>DLE EOT 4 — roll paper sensor.</summary>
    public static byte PaperSensorStatus(PrinterState s)
    {
        byte b = Fixed;
        if (s.Paper >= PaperLevel.NearEnd) b |= 0x0C;  // bits2,3: roll paper near-end
        if (s.Paper == PaperLevel.Out) b |= 0x60;      // bits5,6: roll paper end
        return b;
    }

    /// <summary>GS r 1 — transmit paper sensor status.</summary>
    public static byte TransmitPaperStatus(PrinterState s)
    {
        byte b = 0;
        if (s.Paper >= PaperLevel.NearEnd) b |= 0x03;  // bits0,1: near-end
        if (s.Paper == PaperLevel.Out) b |= 0x0C;      // bits2,3: paper end
        return b;
    }

    /// <summary>GS r 2 — transmit drawer kick-out connector status.</summary>
    public static byte TransmitDrawerStatus(PrinterState s)
        => (byte)(s.DrawerOpen ? 0x01 : 0x00);

    /// <summary>
    /// The 4-byte Automatic Status Back block (sent on GS a / on every state change while enabled).
    /// Byte layout matches the Epson TM ASB format as parsed by ESC-POS-.NET: byte 0 has fixed bit 4
    /// (and bits 0,1,7 clear); the cash-drawer bit is inverted (open = bit 2 clear); paper-low/out
    /// use paired bits.
    /// </summary>
    public static byte[] AutoStatusBack(PrinterState s)
    {
        // Byte 0 — printer info (bit4 fixed 1; bits 0,1,7 fixed 0).
        byte b0 = 0x10;
        if (!s.DrawerOpen) b0 |= 0x04;                // bit2 SET = drawer closed (open => clear)
        if (!s.Online) b0 |= 0x08;                    // bit3 SET = offline
        if (s.CoverOpen) b0 |= 0x20;                  // bit5 = cover open
        if (s.FeedButtonPressed) b0 |= 0x40;          // bit6 = paper currently feeding

        // Byte 1 — error info.
        byte b1 = 0;
        if (s.FeedButtonPressed) b1 |= 0x02;          // bit1 = feed button pushed
        if (s.Error == PrinterErrorState.Unrecoverable) b1 |= 0x20; // bit5
        if (s.Error == PrinterErrorState.Recoverable) b1 |= 0x40;   // bit6

        // Byte 2 — roll paper sensors (paired bits).
        byte b2 = 0;
        if (s.Paper >= PaperLevel.NearEnd) b2 |= 0x03; // bits0,1 = paper low
        if (s.Paper == PaperLevel.Out) b2 |= 0x0C;     // bits2,3 = paper out

        byte b3 = 0;
        return new[] { b0, b1, b2, b3 };
    }
}
