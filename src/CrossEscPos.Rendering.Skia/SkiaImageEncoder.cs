using System.IO;
using CrossEscPos.Graphics;
using SkiaSharp;

namespace CrossEscPos.Rendering.Skia;

/// <summary>SkiaSharp <see cref="IImageEncoder"/> — encodes receipt images to PNG.</summary>
public sealed class SkiaImageEncoder : IImageEncoder
{
    public void EncodePng(IReceiptImage image, Stream destination)
    {
        using var skImage = SKImage.FromBitmap(((SkiaReceiptImage)image).Bitmap);
        using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(destination);
    }

    public byte[] EncodePng(IReceiptImage image)
    {
        using var stream = new MemoryStream();
        EncodePng(image, stream);
        return stream.ToArray();
    }
}
