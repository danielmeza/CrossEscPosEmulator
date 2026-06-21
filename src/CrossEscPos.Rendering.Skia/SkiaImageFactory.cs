using System;
using CrossEscPos.Graphics;
using SkiaSharp;

namespace CrossEscPos.Rendering.Skia;

/// <summary>SkiaSharp <see cref="IReceiptImageFactory"/> — creates SKBitmap-backed images and canvases.</summary>
public sealed class SkiaImageFactory : IReceiptImageFactory
{
    public IReceiptImage Create(int width, int height, ReceiptColor fill)
    {
        var bmp = new SKBitmap(Math.Max(1, width), Math.Max(1, height));
        using (var canvas = new SKCanvas(bmp))
            canvas.Clear(SkiaReceiptImage.ToSk(fill));
        return new SkiaReceiptImage(bmp);
    }

    public IReceiptImage FromPixels(int width, int height, ReceiptColor[] rowMajorPixels)
    {
        var bmp = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        var pixels = new SKColor[width * height];
        int count = Math.Min(rowMajorPixels.Length, pixels.Length);
        for (int i = 0; i < count; i++)
            pixels[i] = SkiaReceiptImage.ToSk(rowMajorPixels[i]);
        bmp.Pixels = pixels;
        return new SkiaReceiptImage(bmp);
    }

    public IReceiptCanvas CreateCanvas(IReceiptImage image)
        => new SkiaReceiptCanvas(new SKCanvas(((SkiaReceiptImage)image).Bitmap));
}
