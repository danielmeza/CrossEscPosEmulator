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
    /// Each byte carries a 2-bit identifier in bits 0-1 (00,01,10,11) per the TM ASB format.
    /// </summary>
    public static byte[] AutoStatusBack(PrinterState s)
    {
        // Byte 1 (id 00): paper feed / online / drawer / cover
        byte b1 = 0;
        if (s.DrawerOpen) b1 |= 0x04;                 // bit2: drawer
        if (!s.Online) b1 |= 0x08;                    // bit3: offline
        if (s.CoverOpen) b1 |= 0x20;                  // bit5: cover open
        if (s.FeedButtonPressed) b1 |= 0x40;          // bit6: paper being fed

        // Byte 2 (id 01): error causes
        byte b2 = 0x01;
        if (s.Error == PrinterErrorState.Recoverable) b2 |= 0x40;
        if (s.Error == PrinterErrorState.Unrecoverable) b2 |= 0x20;

        // Byte 3 (id 10): roll paper sensors
        byte b3 = 0x02;
        if (s.Paper >= PaperLevel.NearEnd) b3 |= 0x0C;
        if (s.Paper == PaperLevel.Out) b3 |= 0x60;

        // Byte 4 (id 11): reserved
        byte b4 = 0x03;

        return new[] { b1, b2, b3, b4 };
    }
}
