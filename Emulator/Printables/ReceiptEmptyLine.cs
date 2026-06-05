using ReceiptPrinterEmulator.Emulator.Abstraction;
using SkiaSharp;

namespace ReceiptPrinterEmulator.Emulator.Printables;

public class ReceiptEmptyLine : IReceiptPrintable
{
    private readonly int _height;

    public ReceiptEmptyLine(int height)
    {
        _height = height;
    }

    public int GetPrintHeight() => _height;

    public void Render(SKCanvas canvas, int offsetX, int offsetY)
    {
    }
}
