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
        => Paint(antialias: true, canvas => canvas.DrawLine(
            new SolidPen(ToColor(color), strokeWidth),
            new PointF(x0, y0),
            new PointF(x1, y1)));

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
