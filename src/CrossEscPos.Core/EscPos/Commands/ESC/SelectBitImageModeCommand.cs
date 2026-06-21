using System.Collections.Generic;
using CrossEscPos.Graphics;
using CrossEscPos.Emulator;

namespace CrossEscPos.EscPos.Commands.ESC;

/// <summary>
/// Select bit-image mode (ESC * m nL nH d1...dk) — the classic inline raster band. m selects the
/// vertical density: 0/1 = 8-dot (1 byte/column), 32/33 = 24-dot (3 bytes/column). Width is
/// nL + nH*256 dot-columns. Each column byte is 8 vertical dots, MSB at top.
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=88
/// </summary>
public class SelectBitImageModeCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.ESC + "*";
    public override bool HasArgs => true;

    private int _index;
    private int _m, _nL, _width, _bytesPerColumn, _dataLen;
    private readonly List<byte> _data = new();

    public override void Reset()
    {
        _index = 0;
        _m = _nL = _width = _bytesPerColumn = _dataLen = 0;
        _data.Clear();
    }

    public override bool InterpretNextChar(char c)
    {
        switch (_index++)
        {
            case 0:
                _m = (byte)c;
                return true;
            case 1:
                _nL = (byte)c;
                return true;
            case 2:
                _width = _nL + ((byte)c << 8);
                _bytesPerColumn = _m is 0 or 1 ? 1 : 3;
                _dataLen = _width * _bytesPerColumn;
                return _dataLen > 0;
            default:
                _data.Add((byte)c);
                return _data.Count < _dataLen;
        }
    }

    public override void Execute(ReceiptPrinter printer, string? args)
    {
        if (_width <= 0 || _data.Count == 0)
            return;

        int height = _bytesPerColumn * 8;
        var pixels = new ReceiptColor[_width * height];
        System.Array.Fill(pixels, ReceiptColor.White);

        for (int col = 0; col < _width; col++)
        {
            for (int byteIdx = 0; byteIdx < _bytesPerColumn; byteIdx++)
            {
                int di = col * _bytesPerColumn + byteIdx;
                if (di >= _data.Count) break;
                byte b = _data[di];
                for (int bit = 0; bit < 8; bit++)
                    if ((b & (0x80 >> bit)) != 0)
                        pixels[(byteIdx * 8 + bit) * _width + col] = ReceiptColor.Black;
            }
        }

        printer.PrintBitmap(printer.ImageFactory.FromPixels(_width, height, pixels));
    }
}
