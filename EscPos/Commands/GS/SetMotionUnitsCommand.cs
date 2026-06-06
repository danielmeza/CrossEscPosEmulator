using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Logging;

namespace ReceiptPrinterEmulator.EscPos.Commands.GS;

/// <summary>
/// Set horizontal and vertical motion units (GS P x y). The emulator renders at a fixed DPI, so the
/// values are accepted and logged but not applied.
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=89
/// </summary>
public class SetMotionUnitsCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "P";
    public override bool HasArgs => true;

    private int _index;
    private int _x, _y;

    public override void Reset()
    {
        _index = 0;
        _x = _y = 0;
    }

    public override bool InterpretNextChar(char c)
    {
        if (_index == 0) _x = (byte)c;
        else _y = (byte)c;
        _index++;
        return _index < 2;
    }

    public override void Execute(ReceiptPrinter printer, string? args)
        => Logger.Info($"Set motion units x={_x} y={_y} (ignored)");
}
