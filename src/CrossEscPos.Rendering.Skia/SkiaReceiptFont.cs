using CrossEscPos.Graphics;
using SkiaSharp;

namespace CrossEscPos.Rendering.Skia;

/// <summary>An <see cref="IReceiptFont"/> backed by an <see cref="SKFont"/> at a fixed pixel size.</summary>
public sealed class SkiaReceiptFont : IReceiptFont
{
    public SKFont Font { get; }

    public SkiaReceiptFont(SKFont font) => Font = font;

    public float Size => Font.Size;

    public FontMetrics Metrics
    {
        get
        {
            var m = Font.Metrics; // Ascent negative, Descent positive — preserved as-is.
            return new FontMetrics(m.Ascent, m.Descent);
        }
    }

    public float MeasureText(string text) => Font.MeasureText(text);

    // Disposes only the SKFont; the underlying typeface is shared/cached by the provider.
    public void Dispose() => Font.Dispose();
}
