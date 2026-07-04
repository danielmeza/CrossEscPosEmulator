using System.Collections.Generic;
using System.Numerics;
using CrossEscPos.Graphics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CrossEscPos.Rendering.ImageSharp;

/// <summary>
/// An <see cref="IReceiptCanvas"/> that draws onto an <see cref="ImageSharpReceiptImage"/>'s backing
/// <see cref="Image{Rgba32}"/>. ImageSharp has no canvas-level save/restore, so the transform is kept
/// as a <see cref="Matrix3x2"/> stack here and applied per draw op via
/// <see cref="AffineTransformBuilder"/>/<c>SetDrawingTransform</c>, mirroring the Skia canvas semantics
/// (translate outermost, scale inner).
/// </summary>
public sealed class ImageSharpReceiptCanvas : IReceiptCanvas
{
    private readonly ImageSharpReceiptImage _target;
    private Matrix3x2 _current = Matrix3x2.Identity;
    private readonly List<Matrix3x2> _stack = new();

    public ImageSharpReceiptCanvas(ImageSharpReceiptImage target) => _target = target;

    private static Color ToColor(ReceiptColor c) => ImageSharpReceiptImage.ToColor(c);

    private void Draw(System.Action<IImageProcessingContext> draw)
    {
        _target.Image.Mutate(ctx =>
        {
            if (_current != Matrix3x2.Identity)
                ctx.SetDrawingTransform(_current);
            draw(ctx);
        });
    }

    public void Clear(ReceiptColor color)
        => _target.Image.Mutate(ctx => ctx.Fill(ToColor(color)));

    public void DrawText(string text, float x, float baselineY, IReceiptFont font, ReceiptColor color)
    {
        var slFont = ((ImageSharpReceiptFont)font).Font;
        // ImageSharp positions text by the layout top-left, so convert baseline → top.
        float ascenderPx = -font.Metrics.Ascent; // Ascent is negative; ascenderPx is positive.
        float top = baselineY - ascenderPx;
        Draw(ctx => ctx.DrawText(
            new RichTextOptions(slFont) { Origin = new PointF(x, top) },
            text,
            ToColor(color)));
    }

    public void DrawRect(ReceiptRect rect, ReceiptColor color)
    {
        // Antialiasing OFF so barcode/QR modules keep crisp, scannable edges — matches the Skia backend
        // (IsAntialias = false). The current transform is carried in the options (explicit DrawingOptions
        // otherwise ignore the context transform set in Draw()).
        var options = new DrawingOptions
        {
            GraphicsOptions = new GraphicsOptions { Antialias = false },
            Transform = _current,
        };
        _target.Image.Mutate(ctx => ctx.Fill(
            options,
            ToColor(color),
            new RectangularPolygon(rect.X, rect.Y, rect.Width, rect.Height)));
    }

    public void DrawLine(float x0, float y0, float x1, float y1, ReceiptColor color, float strokeWidth)
        => Draw(ctx => ctx.DrawLine(
            ToColor(color), strokeWidth, new PointF(x0, y0), new PointF(x1, y1)));

    public void DrawImage(IReceiptImage image, float x, float y)
    {
        var src = ((ImageSharpReceiptImage)image).Image;
        Draw(ctx => ctx.DrawImage(src, new Point((int)x, (int)y), 1f));
    }

    public void DrawImage(IReceiptImage image, ReceiptRect dest)
    {
        var src = ((ImageSharpReceiptImage)image).Image;
        using var scaled = src.Clone(c => c.Resize(
            System.Math.Max(1, (int)dest.Width),
            System.Math.Max(1, (int)dest.Height)));
        Draw(ctx => ctx.DrawImage(scaled, new Point((int)dest.X, (int)dest.Y), 1f));
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
