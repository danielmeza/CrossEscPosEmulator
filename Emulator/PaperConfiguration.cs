using System;
using System.Collections.Generic;
using ReceiptPrinterEmulator.Emulator.Enums;

namespace ReceiptPrinterEmulator.Emulator;

public class PaperConfiguration
{
    private const double MmToInch = 0.0393701;

    public static PaperConfiguration Default => new();

    public double DotsPerInch = 203;
    public double PaperWidthMm = 80;
    public double PrintWidthMm = 76;
    public int DefaultLineSpacing = 10;
    public int DefaultTabSpacing = 8;

    // Render font is a preferred monospace family name. FontProvider resolves it cross-platform,
    // falling back through Consolas / Menlo / DejaVu Sans Mono / Courier New / monospace, or an
    // embedded TTF when present. (Original WPF app used the Windows-only "MS Gothic".)
    private const string RenderFontFamily = "Consolas";

    public Dictionary<PrinterFont, FontConfiguration> _printerFonts = new()
    {
        {PrinterFont.FontA, new FontConfiguration(PrinterFont.FontA, 12, 24, RenderFontFamily)},
        {PrinterFont.FontB, new FontConfiguration(PrinterFont.FontB, 9, 24,  RenderFontFamily)},
        {PrinterFont.FontC, new FontConfiguration(PrinterFont.FontC, 24, 48, RenderFontFamily)},
        {PrinterFont.FontD, new FontConfiguration(PrinterFont.FontD, 16, 24, RenderFontFamily)}
    };

    public FontConfiguration GetFont(PrinterFont printerFont)
    {
        if (_printerFonts.ContainsKey(printerFont))
            return _printerFonts[printerFont];

        if (printerFont != PrinterFont.FontA)
            return GetFont(PrinterFont.FontA);

        throw new InvalidOperationException($"Required font is missing from paper config: {printerFont}");
    }

    public double GetPaperWidthInInches() => PaperWidthMm * MmToInch;
    public double GetPrintWidthInInches() => PrintWidthMm * MmToInch;

    public int GetPaperWidthInPixels() => (int)Math.Ceiling(GetPaperWidthInInches() * DotsPerInch);
    public int GetPrintWidthInPixels() => (int)Math.Ceiling(GetPrintWidthInInches() * DotsPerInch);

    public class FontConfiguration
    {
        public PrinterFont PrinterFont;
        public int CharacterWidth;
        public int CharacterHeight;
        public string RenderFont;

        public FontConfiguration(PrinterFont printerFont, int characterWidth, int characterHeight, string renderFont)
        {
            PrinterFont = printerFont;
            CharacterWidth = characterWidth;
            CharacterHeight = characterHeight;
            RenderFont = renderFont;
        }
    }
}