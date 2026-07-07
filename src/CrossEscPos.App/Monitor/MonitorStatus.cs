namespace CrossEscPos.App.Monitor;

/// <summary>
/// A platform-neutral snapshot of the printer status the emulator reports back. The desktop client maps
/// ESC-POS-.NET's parsed status onto this; the browser client parses the raw 4-byte Automatic Status Back
/// block itself (see <see cref="FromAutoStatusBack"/>), so both drive the same indicator UI.
/// </summary>
public sealed record MonitorStatus(
    bool Online,
    bool PaperOut,
    bool PaperLow,
    bool CoverOpen,
    bool DrawerOpen,
    bool Error)
{
    public bool Ready => Online && !PaperOut && !CoverOpen && !Error;

    /// <summary>
    /// Reverses <c>StatusByteBuilder.AutoStatusBack</c>: interprets the emulator's 4-byte ASB block.
    /// Returns null when the buffer isn't a 4-byte block (e.g. an unrelated response).
    /// </summary>
    public static MonitorStatus? FromAutoStatusBack(byte[] data)
    {
        // The emulator emits ASB as discrete 4-byte frames; if several arrive coalesced, read the last.
        if (data is null || data.Length < 4 || data.Length % 4 != 0)
            return null;

        int off = data.Length - 4;
        byte b0 = data[off], b1 = data[off + 1], b2 = data[off + 2];

        bool drawerOpen = (b0 & 0x04) == 0;  // bit2 SET = drawer closed
        bool online = (b0 & 0x08) == 0;      // bit3 SET = offline
        bool coverOpen = (b0 & 0x20) != 0;   // bit5 = cover open
        bool error = (b1 & 0x60) != 0;       // bits5,6 = recoverable / unrecoverable error
        bool paperOut = (b2 & 0x0C) != 0;    // bits2,3 = paper end
        bool paperLow = !paperOut && (b2 & 0x03) != 0; // bits0,1 = near-end

        return new MonitorStatus(online, paperOut, paperLow, coverOpen, drawerOpen, error);
    }
}
