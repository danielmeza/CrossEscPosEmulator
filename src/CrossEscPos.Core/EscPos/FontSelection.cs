using Ardalis.SmartEnum;
using CrossEscPos.Emulator.Enums;

namespace CrossEscPos.EscPos;

/// <summary>ESC M font codes mapped to <see cref="PrinterFont"/> (numeric/ASCII digit, or 'a'/'b').</summary>
public sealed class FontSelection : SmartEnum<FontSelection>
{
    public static readonly FontSelection FontA    = new(nameof(FontA), 0, PrinterFont.FontA);
    public static readonly FontSelection FontB    = new(nameof(FontB), 1, PrinterFont.FontB);
    public static readonly FontSelection FontC    = new(nameof(FontC), 2, PrinterFont.FontC);
    public static readonly FontSelection FontD    = new(nameof(FontD), 3, PrinterFont.FontD);
    public static readonly FontSelection FontE    = new(nameof(FontE), 4, PrinterFont.FontE);
    public static readonly FontSelection SpecialA = new(nameof(SpecialA), 'a', PrinterFont.SpecialFontA);
    public static readonly FontSelection SpecialB = new(nameof(SpecialB), 'b', PrinterFont.SpecialFontB);

    public PrinterFont Font { get; }

    private FontSelection(string name, int value, PrinterFont font) : base(name, value) => Font = font;

    /// <summary>Resolves an ESC M parameter (numeric or ASCII form); null if unsupported.</summary>
    public static FontSelection? FromParameter(int n)
        => TryFromValue(EscPosParameter.Digit(n), out var selection) ? selection : null;
}
