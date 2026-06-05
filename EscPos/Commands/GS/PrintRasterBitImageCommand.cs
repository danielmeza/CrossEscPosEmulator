using ReceiptPrinterEmulator.Emulator;
using SkiaSharp;

namespace ReceiptPrinterEmulator.EscPos.Commands.GS;

public class PrintRasterBitImageCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "v0";
    public override bool HasArgs => true;

    private int n = 0;
    private int m = 0x00;
    private int xL = 0x00;
    private int xH = 0x00;
    private int width = 0;
    private int yL = 0x00;
    private int yH = 0x00;
    private int height = 0;
    private int length = 0;
    private byte[]? data = null;

    public override bool InterpretNextChar(char c)
    {
        switch (n++)
        {
            case 0:
                m = (int)c;
                return true;
            case 1:
                xL = (int)c;
                return true;
            case 2:
                xH = (int)c;
                width = (xH << 8) | xL;
                return true;
            case 3:
                yL = (int)c;
                return true;
            case 4:
                yH = (int)c;
                height = (yH << 8) | yL;
                length = width * height;
                width *= 8;
                data = new byte[length];
                return length > 0;
            default:
                data![n - 6] = (byte)c;
                return n - 5 < length;
        }
    }

    public override void Reset()
    {
        n = 0;
        m = 0x00;
        xL = 0x00;
        xH = 0x00;
        width = 0;
        yL = 0x00;
        yH = 0x00;
        height = 0;
        length = 0;
        data = null;
    }

    public override void Execute(ReceiptPrinter printer, string? args)
    {
        var bmp = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        var values = ReadBytesByBits(length);

        // Each bit is one pixel: 0 -> white, 1 -> black (row-major).
        var pixels = new SKColor[width * height];
        int count = System.Math.Min(values.Length, pixels.Length);
        for (int i = 0; i < count; i++)
        {
            byte value = values[i] == 0 ? (byte)255 : (byte)0;
            pixels[i] = new SKColor(value, value, value);
        }

        bmp.Pixels = pixels;

        printer.PrintBitmap(bmp);
    }

    private byte[] ReadBytesByBits(int size)
    {
        byte[] result = new byte[size * 8];
        byte b;
        for (int i = 0; i < size; i++)
        {
            b = data![i];
            for (int j = 0; j < 8; j++)
            {
                result[i * 8 + j] = (byte)((b >> (7 - j)) & 1);
            }
        }
        return result;
    }
}
