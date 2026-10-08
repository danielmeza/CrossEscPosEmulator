using System;
using CrossEscPos.Emulator;

namespace CrossEscPos.App.Monitor;

/// <summary>
/// A platform-neutral snapshot of the printer status the emulator reports back. Both clients decode
/// the raw 4-byte Automatic Status Back block with <see cref="AutoStatusBackReader"/> and map it
/// here, so the desktop and browser heads drive the same indicator UI from the same decode.
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
    /// Interprets the emulator's 4-byte ASB block. Returns null when the buffer isn't a whole number
    /// of blocks, or when the last block isn't an ASB block at all (an unrelated response).
    /// </summary>
    public static MonitorStatus? FromAutoStatusBack(byte[] data)
    {
        // The emulator emits ASB as discrete 4-byte frames; if several arrive coalesced, read the last.
        if (data is null || data.Length < AutoStatusBackReader.FrameLength
            || data.Length % AutoStatusBackReader.FrameLength != 0)
            return null;

        return From(AutoStatusBackReader.Parse(
            data.AsSpan(data.Length - AutoStatusBackReader.FrameLength)));
    }

    /// <summary>Maps a decoded status block onto the indicator snapshot; null in, null out.</summary>
    public static MonitorStatus? From(PrinterStatusReport? report)
        => report is null
            ? null
            : new MonitorStatus(report.Online, report.PaperOut, report.PaperLow,
                report.CoverOpen, report.DrawerOpen, report.Error);
}
