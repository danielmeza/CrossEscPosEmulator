using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Emulator.Enums;

namespace ReceiptPrinterEmulator.EscPos.Commands.GS;

/// <summary>
/// Select HRI (Human Readable Interpretation) character print position (GS H n).
/// n: 0/48 = none, 1/49 = above, 2/50 = below, 3/51 = both.
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=123
/// </summary>
public class SelectHriPositionCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "H";
    public override bool HasArgs => true;

    private int _n;

    public override void Reset() => _n = 0;

    public override bool InterpretNextChar(char c)
    {
        _n = (byte)c;
        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args)
    {
        var position = (_n is 1 or 49) ? HriPosition.Above
            : (_n is 2 or 50) ? HriPosition.Below
            : (_n is 3 or 51) ? HriPosition.Both
            : HriPosition.None;

        printer.SetHriPosition(position);
    }
}
