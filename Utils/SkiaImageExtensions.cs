using System.IO;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace ReceiptPrinterEmulator.Utils;

public static class SkiaImageExtensions
{
    /// <summary>
    /// Converts a SkiaSharp <see cref="SKBitmap"/> (used by the receipt renderer) into an Avalonia
    /// <see cref="Bitmap"/> suitable for binding to an <c>Image.Source</c>.
    /// </summary>
    public static Bitmap ToAvaloniaBitmap(this SKBitmap source)
    {
        using var image = SKImage.FromBitmap(source);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    /// <summary>Encodes the bitmap to PNG and writes it to the given stream.</summary>
    public static void SavePng(this SKBitmap source, Stream destination)
    {
        using var image = SKImage.FromBitmap(source);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(destination);
    }
}
