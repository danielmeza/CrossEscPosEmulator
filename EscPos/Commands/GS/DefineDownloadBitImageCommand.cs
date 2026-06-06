using System.Collections.Generic;
using ReceiptPrinterEmulator.Emulator;
using SkiaSharp;

namespace ReceiptPrinterEmulator.EscPos.Commands.GS;

/// <summary>
/// Define downloaded bit image (GS * x y d1...d(x*y*8)). The image is x*8 dots wide and y*8 dots
/// tall. Data is column-major: for each of the x*8 columns there are y vertical bytes (8 dots each,
/// MSB at top). Printed later by GS /.
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=92
/// </summary>
public class DefineDownloadBitImageCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "*";
    public override bool HasArgs => true;

    private int _index;
    private int _x, _y, _dataLen;
    private readonly List<byte> _data = new();

    public override void Reset()
    {
        _index = 0;
        _x = _y = _dataLen = 0;
        _data.Clear();
    }

    public override bool InterpretNextChar(char c)
    {
        switch (_index++)
        {
            case 0:
                _x = (byte)c;
                return true;
            case 1:
                _y = (byte)c;
                _dataLen = _x * _y * 8;
                return _dataLen > 0;
            default:
                _data.Add((byte)c);
                return _data.Count < _dataLen;
        }
    }

    public override void Execute(ReceiptPrinter printer, string? args)
    {
        if (_dataLen == 0 || _data.Count == 0)
            return;

        int width = _x * 8;
        int height = _y * 8;
        var bmp = new SKBitmap(width, height);
        bmp.Erase(SKColors.White);

        for (int col = 0; col < width; col++)
        {
            for (int r = 0; r < _y; r++)
            {
                int di = col * _y + r;
                if (di >= _data.Count) break;
                byte b = _data[di];
                for (int bit = 0; bit < 8; bit++)
                    if ((b & (0x80 >> bit)) != 0)
                        bmp.SetPixel(col, r * 8 + bit, SKColors.Black);
            }
        }

        printer.DefineDownloadBitImage(bmp);
    }
}
