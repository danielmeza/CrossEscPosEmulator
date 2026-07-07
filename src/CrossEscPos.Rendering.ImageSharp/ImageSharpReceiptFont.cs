using CrossEscPos.Graphics;
using SixLabors.Fonts;
using FontMetrics = CrossEscPos.Graphics.FontMetrics;

namespace CrossEscPos.Rendering.ImageSharp;

/// <summary>An <see cref="IReceiptFont"/> backed by a SixLabors <see cref="Font"/> at a fixed pixel size.</summary>
public sealed class ImageSharpReceiptFont : IReceiptFont
{
    internal Font Font { get; }

    public ImageSharpReceiptFont(Font font) => Font = font;

    public float Size => Font.Size;

    public FontMetrics Metrics
    {
        get
        {
            var vertical = Font.FontMetrics.VerticalMetrics;
            float unitsPerEm = Font.FontMetrics.UnitsPerEm;
            // Skia convention: Ascent negative, Descent positive.
            // Ascender is positive in font units → negate for a negative Ascent.
            // Descender is negative in font units → negate for a positive Descent.
            float ascent = -(Font.Size * vertical.Ascender / unitsPerEm);
            float descent = -(Font.Size * vertical.Descender / unitsPerEm);
            return new FontMetrics(ascent, descent);
        }
    }

    private float? _sentinelAdvance;

    // Advance width — matches SkiaSharp's SKFont.MeasureText, which the receipt layout relies on for
    // justification, multi-run advance, and underline length. Two SixLabors quirks are corrected here:
    //   * MeasureSize returns the ink bounding box (drops side bearings) — MeasureAdvance is the advance.
    //   * MeasureAdvance trims *trailing* whitespace, but Skia (and the IReceiptFont contract) counts it;
    //     receipts pad columns with trailing spaces. Appending a non-whitespace sentinel makes those
    //     spaces interior, then we subtract the sentinel's own advance. The receipt font is monospace
    //     with no kerning, so this reproduces Skia's advance exactly.
    public float MeasureText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        const string sentinel = ".";
        var options = new TextOptions(Font);
        _sentinelAdvance ??= TextMeasurer.MeasureAdvance(sentinel, options).Width;
        return TextMeasurer.MeasureAdvance(text + sentinel, options).Width - _sentinelAdvance.Value;
    }

    // SixLabors.Fonts.Font is not IDisposable; the font family is shared/cached by the provider.
    public void Dispose()
    {
    }
}
