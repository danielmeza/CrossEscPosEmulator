using ReceiptPrinterEmulator.Emulator;

namespace ReceiptPrinterEmulator.EscPos.Commands.GS;

/// <summary>
/// Transmit printer ID / information (GS I n). Replies model/type/ROM IDs (single byte) or, for the
/// "info B" variants, a framed name string.
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=69
/// </summary>
public class TransmitPrinterIdCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "I";
    public override bool HasArgs => true;

    private int _n;

    public override void Reset() => _n = 0;

    public override bool InterpretNextChar(char c)
    {
        _n = (byte)c;
        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args) => printer.TransmitPrinterId(_n);
}
