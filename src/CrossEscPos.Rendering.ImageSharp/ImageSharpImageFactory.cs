using System;
using CrossEscPos.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CrossEscPos.Rendering.ImageSharp;

/// <summary>ImageSharp <see cref="IReceiptImageFactory"/> — creates Image&lt;Rgba32&gt;-backed images and canvases.</summary>
public sealed class ImageSharpImageFactory : IReceiptImageFactory
{
    public IReceiptImage Create(int width, int height, ReceiptColor fill)
    {
        var image = new Image<Rgba32>(
            Math.Max(1, width),
            Math.Max(1, height),
            ImageSharpReceiptImage.ToColor(fill));
        return new ImageSharpReceiptImage(image);
    }

    public IReceiptImage FromPixels(int width, int height, ReceiptColor[] rowMajorPixels)
    {
        int w = Math.Max(1, width);
        int h = Math.Max(1, height);
        var image = new Image<Rgba32>(w, h);
        int count = Math.Min(rowMajorPixels.Length, w * h);
        for (int i = 0; i < count; i++)
        {
            var c = rowMajorPixels[i];
            image[i % w, i / w] = new Rgba32(c.R, c.G, c.B, c.A);
        }
        return new ImageSharpReceiptImage(image);
    }

    public IReceiptCanvas CreateCanvas(IReceiptImage image)
        => new ImageSharpReceiptCanvas((ImageSharpReceiptImage)image);
}
