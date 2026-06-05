using ReceiptPrinterEmulator.Emulator.Abstraction;
using ReceiptPrinterEmulator.Logging;
using System;
using SkiaSharp;

namespace ReceiptPrinterEmulator.Emulator.Printables;

public class ReceiptBitmapLine(PaperConfiguration paperConfiguration, SKBitmap image) : IReceiptPrintable
{
    public int GetPrintHeight()
    {
        var printWidth = paperConfiguration.GetPrintWidthInPixels();
        if (image.Width <= printWidth)
            return image.Height;

        return (int)Math.Ceiling(image.Height * (float)printWidth / image.Width);
    }

    public void Render(SKCanvas canvas, int offsetX, int offsetY)
    {
        Logger.Info($"Rendering bitmap line at offset ({offsetX}, {offsetY}) with size ({image.Width}, {image.Height})");

        var printWidth = paperConfiguration.GetPrintWidthInPixels();
        if (image.Width <= printWidth)
        {
            // Center the image horizontally if it fits within the print width
            offsetX += (printWidth - image.Width) / 2;
            canvas.DrawBitmap(image, offsetX, offsetY);
        }
        else
        {
            var scaledHeight = image.Height * (float)printWidth / image.Width;
            var dest = SKRect.Create(offsetX, offsetY, printWidth, scaledHeight);
            using var sampling = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(image, dest, sampling);
        }
    }
}
