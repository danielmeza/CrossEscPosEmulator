using System.Text;
using QRCoder;
using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Logging;

namespace ReceiptPrinterEmulator.EscPos.Commands.GS;

/// <summary>
/// 2D symbol commands (GS ( k pL pH cn fn ...). Only QR Code (cn = 49) is implemented:
///   fn 65: select model     fn 67: module size    fn 69: error correction level
///   fn 80: store data        fn 81: print stored symbol
/// A typical QR is emitted as: set size, set EC, store data, then print.
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=140
/// </summary>
public class PrintQrCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "(k";
    public override bool HasArgs => true;

    private const int QrCode = 49; // cn for QR Code

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
        if (_cn != QrCode)
        {
            Logger.Info($"Unsupported 2D symbol cn={_cn} (only QR Code is supported)");
            return;
        }

        var p = _params.ToString();

        switch (_fn)
        {
            case 65: // select model (n1 n2) — informational; QRCoder is model 2
                Logger.Info($"QR select model: {(p.Length > 0 ? (int)p[0] : 0)}");
                break;

            case 67: // module size
                if (p.Length > 0)
                    printer.SetQrModuleSize((byte)p[0]);
                break;

            case 69: // error correction level
                if (p.Length > 0)
                    printer.SetQrErrorCorrection(MapEcc((byte)p[0]));
                break;

            case 80: // store data in symbol storage area: p[0] = m, rest = data
                printer.StoreQrData(p.Length > 1 ? p.Substring(1) : string.Empty);
                break;

            case 81: // print the symbol data in the storage area
                printer.PrintQr();
                break;

            case 82: // transmit size info — not applicable to an emulator
                break;

            default:
                Logger.Info($"Unsupported QR function fn={_fn}");
                break;
        }
    }

    private static QRCodeGenerator.ECCLevel MapEcc(int n) => n switch
    {
        48 => QRCodeGenerator.ECCLevel.L,
        49 => QRCodeGenerator.ECCLevel.M,
        50 => QRCodeGenerator.ECCLevel.Q,
        51 => QRCodeGenerator.ECCLevel.H,
        _ => QRCodeGenerator.ECCLevel.M
    };
}
