using System.IO;
using Avalonia.Media.Imaging;
using CrossEscPos.Graphics;

namespace CrossEscPos.Controls;

/// <summary>
/// Bridges a backend-agnostic <see cref="IReceiptImage"/> into an Avalonia <see cref="Bitmap"/> via
/// PNG. Going through <see cref="IImageEncoder"/> (rather than SkiaSharp directly) keeps the control
/// library independent of the render backend and works in the browser sandbox.
/// </summary>
public static class AvaloniaImageExtensions
{
    /// <summary>Encodes <paramref name="source"/> to PNG and loads it as an Avalonia <see cref="Bitmap"/>.</summary>
    public static Bitmap ToAvaloniaBitmap(this IReceiptImage source, IImageEncoder encoder)
    {
        using var stream = new MemoryStream(encoder.EncodePng(source));
        return new Bitmap(stream);
    }
}
