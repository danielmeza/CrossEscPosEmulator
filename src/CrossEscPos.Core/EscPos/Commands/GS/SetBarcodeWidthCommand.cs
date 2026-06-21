using CrossEscPos.Emulator;

namespace CrossEscPos.EscPos.Commands.GS;

/// <summary>
/// Set barcode module width (GS w n) — n is the width in dots of the narrow module (typically 2-6).
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=126
/// </summary>
public class SetBarcodeWidthCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "w";
    public override bool HasArgs => true;

    private int _n;

    public override void Reset() => _n = 0;

    public override bool InterpretNextChar(char c)
    {
        _n = (byte)c;
        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args) => printer.SetBarcodeModuleWidth(_n);
}
