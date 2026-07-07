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

    public float MeasureText(string text)
        => TextMeasurer.MeasureSize(text, new TextOptions(Font)).Width;

    // SixLabors.Fonts.Font is not IDisposable; the font family is shared/cached by the provider.
    public void Dispose()
    {
    }
}
