using System.Collections.Generic;
using CrossEscPos.Graphics;
using CrossEscPos.Emulator;

namespace CrossEscPos.EscPos.Commands.ESC;

/// <summary>
/// Define user-defined characters (ESC &amp; y c1 c2 [x d1...d(x*y)]...). For each code c1..c2 a
/// width byte x is followed by x*y column-major bytes (each byte = 8 vertical dots). Glyphs are
/// parsed and stored; see <see cref="ReceiptPrinter.DefineUserGlyph"/>.
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=26
/// </summary>
public class DefineUserDefinedCharsCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.ESC + "&";
    public override bool HasArgs => true;

    private int _phase, _y, _c1, _c2, _x, _dataLeft, _curCode;
    private readonly List<byte> _cur = new();
    private readonly List<(int code, int w, int h, byte[] data)> _defs = new();

    public override void Reset()
    {
        _phase = _y = _c1 = _c2 = _x = _dataLeft = _curCode = 0;
        _cur.Clear();
        _defs.Clear();
    }

    public override bool InterpretNextChar(char c)
    {
        byte v = (byte)c;
        switch (_phase)
        {
            case 0: _y = v; _phase = 1; return true;
            case 1: _c1 = v; _phase = 2; return true;
            case 2:
                _c2 = v;
                _curCode = _c1;
                if (_c2 < _c1) return false;
                _phase = 3;
                return true;
            case 3: // width of the current character
                _x = v;
                _dataLeft = _x * _y;
                _cur.Clear();
                if (_dataLeft == 0)
                {
                    _curCode++;
                    return _curCode <= _c2; // skip empty glyph; read next width or finish
                }
                _phase = 4;
                return true;
            case 4:
                _cur.Add(v);
                _dataLeft--;
                if (_dataLeft == 0)
                {
                    _defs.Add((_curCode, _x, _y, _cur.ToArray()));
                    _curCode++;
                    _phase = 3;
                    return _curCode <= _c2;
                }
                return true;
        }
        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args)
    {
        foreach (var (code, w, h, data) in _defs)
        {
            int width = w;
            int height = h * 8;
            var pixels = new ReceiptColor[width * height];
            System.Array.Fill(pixels, ReceiptColor.White);
            for (int col = 0; col < width; col++)
                for (int r = 0; r < h; r++)
                {
                    int di = col * h + r;
                    if (di >= data.Length) break;
                    byte b = data[di];
                    for (int bit = 0; bit < 8; bit++)
                        if ((b & (0x80 >> bit)) != 0)
                            pixels[(r * 8 + bit) * width + col] = ReceiptColor.Black;
                }
            printer.DefineUserGlyph(code, printer.ImageFactory.FromPixels(width, height, pixels));
        }
    }
}

/// <summary>Select/cancel user-defined character set (ESC % n) — bit 0 enables.</summary>
public class EnableUserDefinedCharsCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.ESC + "%";
    public override bool HasArgs => true;

    private int _n;
    public override void Reset() => _n = 0;
    public override bool InterpretNextChar(char c) { _n = (byte)c; return false; }
    public override void Execute(ReceiptPrinter printer, string? args) => printer.EnableUserDefined((_n & 1) != 0);
}

/// <summary>Cancel a user-defined character (ESC ? n).</summary>
public class CancelUserDefinedCharCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.ESC + "?";
    public override bool HasArgs => true;

    private int _n;
    public override void Reset() => _n = 0;
    public override bool InterpretNextChar(char c) { _n = (byte)c; return false; }
    public override void Execute(ReceiptPrinter printer, string? args) => printer.CancelUserGlyph(_n);
}
