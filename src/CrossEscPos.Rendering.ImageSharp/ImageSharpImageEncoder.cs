using System.IO;
using CrossEscPos.Graphics;
using SixLabors.ImageSharp;

namespace CrossEscPos.Rendering.ImageSharp;

/// <summary>ImageSharp <see cref="IImageEncoder"/> — encodes receipt images to PNG.</summary>
public sealed class ImageSharpImageEncoder : IImageEncoder
{
    public void EncodePng(IReceiptImage image, Stream destination)
        => ((ImageSharpReceiptImage)image).Image.SaveAsPng(destination);

    public byte[] EncodePng(IReceiptImage image)
    {
        using var stream = new MemoryStream();
        EncodePng(image, stream);
        return stream.ToArray();
    }
}
