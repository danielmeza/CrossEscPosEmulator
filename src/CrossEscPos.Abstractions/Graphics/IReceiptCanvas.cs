using System;

namespace CrossEscPos.Graphics;

/// <summary>
/// A 2D drawing surface over an <see cref="IReceiptImage"/>. This is the seam that lets the render
/// backend be swapped: the receipt document model (text lines, bit images, barcodes) draws against
/// this interface and never touches SkiaSharp directly.
///
/// Anti-aliasing is fixed per primitive to match the original Skia output: text and lines are
/// anti-aliased (smooth glyphs / underlines), filled rectangles are not (crisp barcode modules), and
/// scaled image draws are sampled.
///
/// The canvas is created by <see cref="IReceiptImageFactory.CreateCanvas"/>; the creator owns and
/// disposes it. Elements handed a canvas in <see cref="IReceiptPrintable.Render"/> only draw — they
/// must not dispose it.
/// </summary>
public interface IReceiptCanvas : IDisposable
{
    /// <summary>Fills the entire surface with <paramref name="color"/>.</summary>
    void Clear(ReceiptColor color);

    /// <summary>Draws <paramref name="text"/> with its baseline at (<paramref name="x"/>, <paramref name="baselineY"/>).</summary>
    void DrawText(string text, float x, float baselineY, IReceiptFont font, ReceiptColor color);

    /// <summary>Fills <paramref name="rect"/> (no anti-aliasing — crisp module edges).</summary>
    void DrawRect(ReceiptRect rect, ReceiptColor color);

    /// <summary>Strokes a line of the given width (anti-aliased).</summary>
    void DrawLine(float x0, float y0, float x1, float y1, ReceiptColor color, float strokeWidth);

    /// <summary>Draws <paramref name="image"/> at (<paramref name="x"/>, <paramref name="y"/>) at native size.</summary>
    void DrawImage(IReceiptImage image, float x, float y);

    /// <summary>Draws <paramref name="image"/> scaled to fill <paramref name="dest"/> (sampled).</summary>
    void DrawImage(IReceiptImage image, ReceiptRect dest);

    /// <summary>Saves the current transform and returns a restore token (see <see cref="RestoreToCount"/>).</summary>
    int Save();

    void Translate(float dx, float dy);

    void Scale(float sx, float sy);

    /// <summary>Restores the transform stack to the depth returned by an earlier <see cref="Save"/>.</summary>
    void RestoreToCount(int count);

    /// <summary>Flushes any buffered drawing onto the backing image.</summary>
    void Flush();
}
