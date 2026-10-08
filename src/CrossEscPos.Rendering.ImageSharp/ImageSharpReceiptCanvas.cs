using System;
using System.Collections.Generic;
using System.Numerics;
using CrossEscPos.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CrossEscPos.Rendering.ImageSharp;

/// <summary>
/// An <see cref="IReceiptCanvas"/> that draws onto an <see cref="ImageSharpReceiptImage"/>'s backing
/// <see cref="Image{Rgba32}"/>.
///
/// ImageSharp.Drawing 3.x replaced the per-context drawing extensions (<c>DrawText</c>, <c>Fill</c>,
/// <c>DrawLine</c>, and the ambient <c>SetDrawingTransform</c>) with a retained-mode
/// <see cref="DrawingCanvas"/> entered through <see cref="PaintExtensions.Paint(IImageProcessingContext,
/// DrawingOptions, CanvasAction)"/>. That canvas is scoped to one paint pass, so the receipt-level
/// transform stack stays here as a <see cref="Matrix3x2"/> stack and is handed to each pass through
/// <see cref="DrawingOptions.Transform"/> — mirroring the Skia canvas semantics
/// (translate outermost, scale inner).
///
/// Two side effects of that move are deliberate, and both bring this backend closer to the Skia one:
/// image draws now honour the canvas transform (ImageSharp 3.x's image processor ignored the ambient
/// drawing transform entirely), and a fractional image origin is resampled rather than truncated to
/// whole pixels, as <c>SKCanvas.DrawBitmap</c> does. No current call site draws an image under a
/// non-identity transform or at a fractional origin, so neither changes today's output.
/// </summary>
public sealed class ImageSharpReceiptCanvas : IReceiptCanvas
{
    private readonly ImageSharpReceiptImage _target;
    private Matrix3x2 _current = Matrix3x2.Identity;
    private readonly List<Matrix3x2> _stack = new();

    public ImageSharpReceiptCanvas(ImageSharpReceiptImage target) => _target = target;

    private static Color ToColor(ReceiptColor c) => ImageSharpReceiptImage.ToColor(c);

    /// <summary>
    /// Runs one paint pass with the current transform and the given anti-aliasing applied.
    /// Anti-aliasing is fixed per primitive to match the Skia backend's output: text, lines and
    /// sampled images are smoothed, filled rectangles are not.
    /// </summary>
    private void Paint(bool antialias, Action<DrawingCanvas> draw)
    {
        var options = new DrawingOptions
        {
            GraphicsOptions = new GraphicsOptions { Antialias = antialias },
            // Drawing 3.x widened the drawing transform from Matrix3x2 to Matrix4x4. Matrix4x4 has a
            // Matrix3x2 constructor, so the 2D stack kept here promotes without loss.
            Transform = new Matrix4x4(_current),
        };

        _target.Image.Mutate(ctx => ctx.Paint(options, canvas => draw(canvas)));
    }

    /// <summary>Fills the whole surface. Deliberately untransformed — it clears device pixels.</summary>
    public void Clear(ReceiptColor color)
        => _target.Image.Mutate(ctx => ctx.Paint(canvas => canvas.Fill(new SolidBrush(ToColor(color)))));

    public void DrawText(string text, float x, float baselineY, IReceiptFont font, ReceiptColor color)
    {
        var slFont = ((ImageSharpReceiptFont)font).Font;
        // ImageSharp positions text by the layout top-left, so convert baseline → top.
        float ascenderPx = -font.Metrics.Ascent; // Ascent is negative; ascenderPx is positive.
        float top = baselineY - ascenderPx;
        var options = new RichTextOptions(slFont) { Origin = new PointF(x, top) };

        Paint(antialias: true, canvas => canvas.DrawText(options, text, new SolidBrush(ToColor(color)), null));
    }

    public void DrawRect(ReceiptRect rect, ReceiptColor color)
        // Antialiasing OFF so barcode/QR modules keep crisp, scannable edges — matches the Skia
        // backend (IsAntialias = false). RectangularPolygon was renamed RectanglePolygon in Drawing 3.x.
        => Paint(antialias: false, canvas => canvas.Fill(
            new SolidBrush(ToColor(color)),
            new RectanglePolygon(rect.X, rect.Y, rect.Width, rect.Height)));

    public void DrawLine(float x0, float y0, float x1, float y1, ReceiptColor color, float strokeWidth)
    {
        // Drawing 3.x centres a stroke on its geometric coordinate, so an axis-aligned odd-width line
        // lands exactly on a pixel boundary and rasterises as two half-covered rows at 50% grey
        // instead of one solid row. A receipt underline is a dot row, and 50% grey vanishes under the
        // 1-bit threshold a thermal printer applies — measured: a one-dot underline produced zero
        // pixels below 50% luminance, while the Skia backend produces a solid run.
        //
        // An axis-aligned line is therefore filled as a hard-edged rectangle of the same extent, which
        // is what the dot row physically is. Doing it as geometry rather than as a half-pixel nudge
        // keeps it correct under the canvas transform: a double-height text run scales the rectangle,
        // where a nudge in local coordinates would scale into a full-pixel displacement.
        var brush = new SolidBrush(ToColor(color));
        float half = strokeWidth / 2f;

        if (y0 == y1)
        {
            float x = Math.Min(x0, x1);
            Paint(antialias: false, canvas => canvas.Fill(
                brush, new RectanglePolygon(x, y0 - half, Math.Abs(x1 - x0), strokeWidth)));
        }
        else if (x0 == x1)
        {
            float y = Math.Min(y0, y1);
            Paint(antialias: false, canvas => canvas.Fill(
                brush, new RectanglePolygon(x0 - half, y, strokeWidth, Math.Abs(y1 - y0))));
        }
        else
        {
            // Diagonals have no pixel grid to snap to; stroke them anti-aliased, as the contract says.
            Paint(antialias: true, canvas => canvas.DrawLine(
                new SolidPen(ToColor(color), strokeWidth), new PointF(x0, y0), new PointF(x1, y1)));
        }
    }

    public void DrawImage(IReceiptImage image, float x, float y)
    {
        var src = ((ImageSharpReceiptImage)image).Image;
        Paint(antialias: true, canvas => canvas.DrawImage(
            src,
            src.Bounds,
            new RectangleF(x, y, src.Width, src.Height)));
    }

    public void DrawImage(IReceiptImage image, ReceiptRect dest)
    {
        var src = ((ImageSharpReceiptImage)image).Image;
        // Drawing 3.x resamples source → destination itself, so the old clone-and-Resize step is gone.
        Paint(antialias: true, canvas => canvas.DrawImage(
            src,
            src.Bounds,
            new RectangleF(dest.X, dest.Y, Math.Max(1f, dest.Width), Math.Max(1f, dest.Height))));
    }

    public int Save()
    {
        _stack.Add(_current);
        return _stack.Count;
    }

    public void Translate(float dx, float dy)
        => _current = Matrix3x2.CreateTranslation(dx, dy) * _current;

    public void Scale(float sx, float sy)
        => _current = Matrix3x2.CreateScale(sx, sy) * _current;

    public void RestoreToCount(int count)
    {
        while (_stack.Count >= count && _stack.Count > 0)
        {
            _current = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
        }
    }

    public void Flush()
    {
    }

    // Does not own the backing image; nothing to dispose.
    public void Dispose()
    {
    }
}
