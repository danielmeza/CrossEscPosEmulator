using SkiaSharp;

namespace ReceiptPrinterEmulator.Emulator.Abstraction;

public interface IReceiptPrintable
{
    /// <summary>
    /// Draws this line onto the receipt canvas at the given top-left offset.
    /// </summary>
    public void Render(SKCanvas canvas, int offsetX, int offsetY);

    public int GetPrintHeight();
}
