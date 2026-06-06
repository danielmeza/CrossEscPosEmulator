using ReceiptPrinterEmulator.Emulator;

namespace ReceiptPrinterEmulator.EscPos.Commands.GS;

/// <summary>
/// Print downloaded bit image (GS / m). m selects scaling: 0 normal, 1 double-width,
/// 2 double-height, 3 quadruple.
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=93
/// </summary>
public class PrintDownloadBitImageCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "/";
    public override bool HasArgs => true;

    private int _m;

    public override void Reset() => _m = 0;

    public override bool InterpretNextChar(char c)
    {
        _m = (byte)c;
        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args) => printer.PrintDownloadBitImage(_m);
}
