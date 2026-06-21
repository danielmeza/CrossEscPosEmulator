using System.Text;

namespace CrossEscPos.Core.Tests;

/// <summary>
/// Builds raw ESC/POS byte strings (one char per byte, matching how the interpreter consumes input)
/// for the end-to-end emulator tests.
/// </summary>
internal static class EscPosSequence
{
    public const int ESC = 0x1B, GS = 0x1D, FS = 0x1C, DLE = 0x10;
    public const int NUL = 0x00, BEL = 0x07, HT = 0x09, LF = 0x0A, FF = 0x0C, CR = 0x0D, CAN = 0x18;
    public const int EOT = 0x04, ENQ = 0x05, DC4 = 0x14;

    /// <summary>Packs byte values (chars are fine — they convert) into a one-char-per-byte string.</summary>
    public static string Bytes(params int[] values)
    {
        var sb = new StringBuilder(values.Length);
        foreach (var v in values)
            sb.Append((char)(v & 0xFF));
        return sb.ToString();
    }

    public static string Text(string s) => s;

    /// <summary>GS k Function A: print a 1D barcode (NUL-terminated data).</summary>
    public static string Barcode(int systemA, string data) => Bytes(GS, 'k', systemA) + data + Bytes(NUL);

    /// <summary>GS ( k: store then print a 2D symbol of family <paramref name="cn"/>.</summary>
    public static string Symbol2D(int cn, string data)
    {
        const int store = 80, print = 81, m = 48;
        int len = 3 + data.Length; // cn + fn + m + data
        return Bytes(GS, '(', 'k', len & 0xFF, len >> 8, cn, store, m) + data
             + Bytes(GS, '(', 'k', 3, 0, cn, print, m);
    }

    /// <summary>GS v 0: a minimal 8x1 raster bit image.</summary>
    public static string Raster() => Bytes(GS, 'v', '0', 0, 1, 0, 1, 0, 0xFF);
}
