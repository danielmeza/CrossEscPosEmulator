using CrossEscPos.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CrossEscPos.Rendering.ImageSharp;

/// <summary>An <see cref="IReceiptImage"/> backed by an <see cref="Image{Rgba32}"/>.</summary>
public sealed class ImageSharpReceiptImage : IReceiptImage
{
    internal Image<Rgba32> Image { get; }

    public ImageSharpReceiptImage(Image<Rgba32> image) => Image = image;

    public int Width => Image.Width;
    public int Height => Image.Height;

    public IReceiptImage Copy() => new ImageSharpReceiptImage(Image.Clone());

    public void Dispose() => Image.Dispose();

    internal static Color ToColor(ReceiptColor c) => Color.FromRgba(c.R, c.G, c.B, c.A);
}
