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

    /// <summary>
    /// The single <see cref="ReceiptColor"/> → ImageSharp conversion seam.
    /// ImageSharp 4.x removed <c>Color.FromRgba(byte, byte, byte, byte)</c>; the pixel-typed
    /// <see cref="Color.FromPixel{TPixel}(TPixel)"/> is the supported way in.
    /// </summary>
    internal static Color ToColor(ReceiptColor c) => Color.FromPixel(ToPixel(c));

    /// <summary>
    /// The pixel form of <see cref="ToColor"/>, for the APIs that take a <c>TPixel</c> rather than a
    /// <see cref="Color"/>. ImageSharp 3.x let a <see cref="Color"/> convert to
    /// <see cref="Rgba32"/> implicitly; 4.x removed that operator, leaving
    /// <see cref="Color.ToPixel{TPixel}()"/> — which this bypasses, since the components are already
    /// 8-bit RGBA and need no round trip through <see cref="Color"/>.
    /// </summary>
    internal static Rgba32 ToPixel(ReceiptColor c) => new(c.R, c.G, c.B, c.A);
}
