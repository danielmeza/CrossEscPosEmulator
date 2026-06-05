using ReceiptPrinterEmulator.Emulator;

namespace ReceiptPrinterEmulator.EscPos.Commands.GS;

/// <summary>
/// Set barcode height (GS h n)
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=125
/// </summary>
public class SetBarcodeHeightCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "h";
    public override bool HasArgs => true;

    private int _n;

    public override void Reset() => _n = 0;

    public override bool InterpretNextChar(char c)
    {
        _n = (byte)c;
        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args) => printer.SetBarcodeHeight(_n);
}
