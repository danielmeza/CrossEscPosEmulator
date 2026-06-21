using CrossEscPos.Graphics;
using SkiaSharp;

namespace CrossEscPos.Rendering.Skia;

/// <summary>An <see cref="IReceiptImage"/> backed by an <see cref="SKBitmap"/>.</summary>
public sealed class SkiaReceiptImage : IReceiptImage
{
    public SKBitmap Bitmap { get; }

    public SkiaReceiptImage(SKBitmap bitmap) => Bitmap = bitmap;

    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;

    public IReceiptImage Copy() => new SkiaReceiptImage(Bitmap.Copy());

    public void Dispose() => Bitmap.Dispose();

    internal static SKColor ToSk(ReceiptColor c) => new(c.R, c.G, c.B, c.A);
}
