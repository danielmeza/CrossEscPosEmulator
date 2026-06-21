using System.Text;
using CrossEscPos.Emulator;
using CrossEscPos.Logging;

namespace CrossEscPos.EscPos.Commands.GS;

/// <summary>
/// Print 1D barcode (GS k). Supports both ESC/POS forms:
///   Function A: GS k m d1...dk NUL          (m = 0..6, NUL-terminated data)
///   Function B: GS k m n d1...dn            (m = 65..73, n = data length)
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=88
/// </summary>
public class PrintBarcodeCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "k";
    public override bool HasArgs => true;

    private enum Phase { System, LengthB, DataA, DataB }

    private Phase _phase;
    private int _m;
    private int _remaining;
    private readonly StringBuilder _data = new();

    public override void Reset()
    {
        _phase = Phase.System;
        _m = 0;
        _remaining = 0;
        _data.Clear();
    }

    public override bool InterpretNextChar(char c)
    {
        switch (_phase)
        {
            case Phase.System:
                _m = (byte)c;
                bool formB = _m is >= 65 and <= 73;
                _phase = formB ? Phase.LengthB : Phase.DataA;
                return true;

            case Phase.LengthB:
                _remaining = (byte)c;
                _phase = Phase.DataB;
                return _remaining > 0;

            case Phase.DataA:
                if (c == EscPosInterpreter.NUL)
                    return false;
                _data.Append(c);
                return true;

            case Phase.DataB:
                _data.Append(c);
                _remaining--;
                return _remaining > 0;
        }

        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args)
    {
        var system = Barcode1DSystem.FromCommandCode(_m);
        if (system is null)
        {
            Logger.Info($"Unsupported barcode system m={_m}");
            return;
        }

        printer.PrintBarcode(system.Format, _data.ToString());
    }
}
