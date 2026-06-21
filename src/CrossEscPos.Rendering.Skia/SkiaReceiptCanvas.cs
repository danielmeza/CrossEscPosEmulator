using System;
using CrossEscPos.Graphics;
using SkiaSharp;

namespace CrossEscPos.Rendering.Skia;

/// <summary>
/// An <see cref="IReceiptCanvas"/> backed by an <see cref="SKCanvas"/>. Per-primitive anti-aliasing
/// matches the original direct-Skia receipt rendering exactly (see <see cref="IReceiptCanvas"/>).
/// </summary>
public sealed class SkiaReceiptCanvas : IReceiptCanvas
{
    private readonly SKCanvas _canvas;
    private readonly bool _ownsCanvas;

    public SkiaReceiptCanvas(SKCanvas canvas, bool ownsCanvas = true)
    {
        _canvas = canvas;
        _ownsCanvas = ownsCanvas;
    }

    public void Clear(ReceiptColor color) => _canvas.Clear(SkiaReceiptImage.ToSk(color));

    public void DrawText(string text, float x, float baselineY, IReceiptFont font, ReceiptColor color)
    {
        using var paint = new SKPaint { Color = SkiaReceiptImage.ToSk(color), IsAntialias = true };
        _canvas.DrawText(text, x, baselineY, ((SkiaReceiptFont)font).Font, paint);
    }

    public void DrawRect(ReceiptRect rect, ReceiptColor color)
    {
        using var paint = new SKPaint { Color = SkiaReceiptImage.ToSk(color), IsAntialias = false };
        _canvas.DrawRect(SKRect.Create(rect.X, rect.Y, rect.Width, rect.Height), paint);
    }

    public void DrawLine(float x0, float y0, float x1, float y1, ReceiptColor color, float strokeWidth)
    {
        using var paint = new SKPaint
        {
            Color = SkiaReceiptImage.ToSk(color),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth
        };
        _canvas.DrawLine(x0, y0, x1, y1, paint);
    }

    public void DrawImage(IReceiptImage image, float x, float y)
        => _canvas.DrawBitmap(((SkiaReceiptImage)image).Bitmap, x, y);

    public void DrawImage(IReceiptImage image, ReceiptRect dest)
    {
        using var paint = new SKPaint { IsAntialias = true };
        _canvas.DrawBitmap(((SkiaReceiptImage)image).Bitmap,
            SKRect.Create(dest.X, dest.Y, dest.Width, dest.Height), paint);
    }

    public int Save() => _canvas.Save();
    public void Translate(float dx, float dy) => _canvas.Translate(dx, dy);
    public void Scale(float sx, float sy) => _canvas.Scale(sx, sy);
    public void RestoreToCount(int count) => _canvas.RestoreToCount(count);
    public void Flush() => _canvas.Flush();

    public void Dispose()
    {
        if (_ownsCanvas)
            _canvas.Dispose();
    }
}
