using System;
using CrossEscPos;
using CrossEscPos.Graphics;
using CrossEscPos.Logging;

namespace CrossEscPos.Emulator.Printables;

public class ReceiptBitmapLine(PaperConfiguration paperConfiguration, IReceiptImage image) : IReceiptPrintable
{
    public int GetPrintHeight()
    {
        var printWidth = paperConfiguration.GetPrintWidthInPixels();
        if (image.Width <= printWidth)
            return image.Height;

        return (int)Math.Ceiling(image.Height * (float)printWidth / image.Width);
    }

    public void Render(IReceiptCanvas canvas, int offsetX, int offsetY)
    {
        Logger.Info($"Rendering bitmap line at offset ({offsetX}, {offsetY}) with size ({image.Width}, {image.Height})");

        var printWidth = paperConfiguration.GetPrintWidthInPixels();
        if (image.Width <= printWidth)
        {
            // Center the image horizontally if it fits within the print width
            offsetX += (printWidth - image.Width) / 2;
            canvas.DrawImage(image, offsetX, offsetY);
        }
        else
        {
            var scaledHeight = image.Height * (float)printWidth / image.Width;
            canvas.DrawImage(image, ReceiptRect.Create(offsetX, offsetY, printWidth, scaledHeight));
        }
    }
}
