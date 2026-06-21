using System.Text;
using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Rendering;
using CrossEscPos.Logging;

namespace CrossEscPos.EscPos.Commands.GS;

/// <summary>
/// 2D symbol commands (GS ( k pL pH cn fn ...). Supports QR Code (cn=49), PDF417 (cn=48),
/// DataMatrix (cn=54) and Aztec (cn=55):
///   fn 67: module size    fn 69: error correction level (QR)
///   fn 80: store data      fn 81: print stored symbol
/// A typical symbol is emitted as: set size, (set EC), store data, then print.
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=140
/// </summary>
public class PrintQrCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "(k";
    public override bool HasArgs => true;

    private enum Phase { PL, PH, CN, FN, Params }

    private Phase _phase;
    private int _pL;
    private int _remaining;
    private int _cn;
    private int _fn;
    private readonly StringBuilder _params = new();

    public override void Reset()
    {
        _phase = Phase.PL;
        _pL = 0;
        _remaining = 0;
        _cn = 0;
        _fn = 0;
        _params.Clear();
    }

    public override bool InterpretNextChar(char c)
    {
        switch (_phase)
        {
            case Phase.PL:
                _pL = (byte)c;
                _phase = Phase.PH;
                return true;

            case Phase.PH:
                _remaining = _pL + ((byte)c << 8); // counts cn, fn and parameters
                _phase = Phase.CN;
                return _remaining > 0;

            case Phase.CN:
                _cn = (byte)c;
                _remaining--;
                _phase = Phase.FN;
                return _remaining > 0;

            case Phase.FN:
                _fn = (byte)c;
                _remaining--;
                _phase = Phase.Params;
                return _remaining > 0;

            case Phase.Params:
                _params.Append(c);
                _remaining--;
                return _remaining > 0;
        }

        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args)
    {
        if (!TwoDimensionCode.TryFromValue(_cn, out _))
        {
            Logger.Info($"Unsupported 2D symbol cn={_cn}");
            return;
        }

        if (TwoDSymbolFunction.TryFromValue(_fn, out var function))
            function.Apply(printer, _cn, _params.ToString());
        else
            Logger.Info($"Unsupported 2D function fn={_fn}");
    }
}
