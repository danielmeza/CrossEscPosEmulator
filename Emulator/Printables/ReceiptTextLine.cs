using System;
using System.Collections.Generic;
using ReceiptPrinterEmulator.Emulator.Abstraction;
using ReceiptPrinterEmulator.Emulator.Enums;
using ReceiptPrinterEmulator.Emulator.Rendering;
using SkiaSharp;

namespace ReceiptPrinterEmulator.Emulator.Printables;

public class ReceiptTextLine : IReceiptPrintable
{
    // GDI+ Font sizes were specified in points and rendered onto a 96-DPI bitmap, so the original
    // app effectively used an em size of (points * 96/72) pixels. We keep that ratio so receipt
    // text keeps the same proportions now that SkiaSharp font sizes are specified in pixels.
    private const float PointsToPixels = 96f / 72f;

    private readonly PaperConfiguration.FontConfiguration _font;
    private readonly int _printWidth;
    private readonly int _charHeight;
    private readonly TextJustification _justification;
    private readonly bool _bold;
    private readonly bool _italic;
    private readonly UnderlineMode _underline;

    private int _totalWidth;
    private readonly List<(string text, PrintMode mode)> _strings = new();

    public bool IsEmpty => _strings.Count == 0;

    public ReceiptTextLine(PaperConfiguration paperConfiguration, PrintMode printMode)
    {
        _font = paperConfiguration.GetFont(printMode.Font);
        _printWidth = paperConfiguration.GetPrintWidthInPixels();
        _charHeight = _font.CharacterHeight * printMode.CharHeightScale;
        _justification = printMode.Justification;
        _bold = printMode.Emphasize;
        _italic = printMode.Italic;
        _underline = printMode.Underline;

        _totalWidth = 0;
    }

    public bool TryWriteChar(char c, PrintMode mode)
    {
        int charWidth = (_font.CharacterWidth * mode.CharWidthScale);
        if ((_totalWidth + charWidth) >= _printWidth)
            return false;

        if (_strings.Count > 0 && mode.Equals(_strings[^1].mode))
        {
            // Append to last run
            var (text, lastMode) = _strings[^1];
            _strings[^1] = (text + c, lastMode);
        }
        else
        {
            // Start new run
            _strings.Add((c.ToString(), mode.Clone()));
        }
        _totalWidth += charWidth;
        return true;
    }

    public int GetPrintHeight()
    {
        // Use the tallest run's height for correct line spacing
        int maxCharHeight = 0;
        foreach (var (_, mode) in _strings)
        {
            int charHeight = (_font.CharacterHeight / 2) * mode.CharHeightScale;
            if (charHeight > maxCharHeight)
                maxCharHeight = charHeight;
        }
        // Add a small extra space for visual separation (like real printers)
        return maxCharHeight + (_font.CharacterHeight / 4);
    }

    private SKFont CreateFont(PrintMode mode)
    {
        float sizePx = (_font.CharacterHeight / 2f) * PointsToPixels;
        var typeface = FontProvider.Get(_font.RenderFont, mode.Emphasize, mode.Italic);
        return new SKFont(typeface, sizePx);
    }

    public void Render(SKCanvas canvas, int offsetX, int offsetY)
    {
        if (_strings.Count == 0)
            return;

        using var fillPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        // 1. Measure each run (unscaled) and the total justified width.
        var runFonts = new List<SKFont>(_strings.Count);
        var runWidths = new List<float>(_strings.Count);
        float totalWidth = 0;
        try
        {
            foreach (var (text, mode) in _strings)
            {
                var font = CreateFont(mode);
                runFonts.Add(font);
                float measured = font.MeasureText(text);
                float scaledWidth = measured * mode.CharWidthScale;
                runWidths.Add(scaledWidth);
                totalWidth += scaledWidth;
            }

            // 2. Justification (taken from the first run, an ESC/POS line property).
            TextJustification justification = _strings[0].mode.Justification;
            int x = offsetX;
            if (justification == TextJustification.Center)
                x += (int)((_printWidth - totalWidth) / 2);
            else if (justification == TextJustification.Right)
                x += (int)(_printWidth - totalWidth);

            // 3. Find the tallest run for baseline alignment (device pixels).
            float maxAscent = 0;
            float maxCharHeight = 0;
            for (int i = 0; i < _strings.Count; i++)
            {
                var mode = _strings[i].mode;
                var metrics = runFonts[i].Metrics;
                float ascent = -metrics.Ascent * mode.CharHeightScale;
                float charHeight = runFonts[i].Size * mode.CharHeightScale;
                if (ascent > maxAscent) maxAscent = ascent;
                if (charHeight > maxCharHeight) maxCharHeight = charHeight;
            }

            // 4. Draw each run, baseline-aligned, applying width/height scale via the canvas.
            for (int i = 0; i < _strings.Count; i++)
            {
                var (text, mode) = _strings[i];
                var font = runFonts[i];
                var metrics = font.Metrics;

                float ascent = -metrics.Ascent;                          // unscaled, device px
                float scaledAscent = ascent * mode.CharHeightScale;
                float scaledCharHeight = font.Size * mode.CharHeightScale;
                float baselineOffset = (maxAscent - scaledAscent) + (maxCharHeight - scaledCharHeight);

                int count = canvas.Save();
                // Translate (device px) then scale, mirroring the original GDI+ transform order.
                canvas.Translate(x, offsetY + baselineOffset);
                canvas.Scale(mode.CharWidthScale, mode.CharHeightScale);

                // Skia positions text on the baseline; draw at y = ascent so the glyph top sits at 0.
                canvas.DrawText(text, 0, ascent, font, fillPaint);

                // Underline drawn in the scaled context, just below the glyph box.
                if (mode.Underline is UnderlineMode.OnOneDot or UnderlineMode.OnTwoDots)
                {
                    float dotHeight = mode.Underline is UnderlineMode.OnTwoDots ? 2 : 1;
                    float underlineY = ascent + metrics.Descent;
                    float runWidthUnscaled = font.MeasureText(text);
                    using var underlinePaint = new SKPaint
                    {
                        Color = SKColors.Black,
                        IsAntialias = true,
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = dotHeight
                    };
                    canvas.DrawLine(0, underlineY, runWidthUnscaled, underlineY, underlinePaint);
                }

                canvas.RestoreToCount(count);

                x += (int)Math.Ceiling(runWidths[i]);
            }
        }
        finally
        {
            foreach (var font in runFonts)
                font.Dispose();
        }
    }
}
