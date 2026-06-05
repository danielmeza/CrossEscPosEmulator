using ReceiptPrinterEmulator.Emulator;

namespace ReceiptPrinterEmulator.EscPos.Commands.ESC;

/// <summary>
/// Generate pulse / kick the cash drawer (ESC p m t1 t2).
/// m selects the drawer-kick connector pin; t1/t2 are on/off pulse times (ignored by the emulator).
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=66
/// </summary>
public class GeneratePulseCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.ESC + "p";
    public override bool HasArgs => true;

    private int _index;
    private int _m;

    public override void Reset()
    {
        _index = 0;
        _m = 0;
    }

    public override bool InterpretNextChar(char c)
    {
        if (_index == 0)
            _m = (byte)c;

        _index++;
        return _index < 3; // consume m, t1, t2
    }

    public override void Execute(ReceiptPrinter printer, string? args) => printer.KickCashDrawer(_m);
}
