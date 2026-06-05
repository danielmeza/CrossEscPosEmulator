using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Emulator.Enums;

namespace ReceiptPrinterEmulator.EscPos.Commands.GS;

/// <summary>
/// Select font for HRI characters (GS f n). n: 0/48 = Font A, 1/49 = Font B.
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=122
/// </summary>
public class SelectHriFontCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "f";
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
        var font = (_n is 1 or 49) ? PrinterFont.FontB : PrinterFont.FontA;
        printer.SetHriFont(font);
    }
}
