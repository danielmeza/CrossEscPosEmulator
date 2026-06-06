using ReceiptPrinterEmulator.Emulator;

namespace ReceiptPrinterEmulator.EscPos.Commands.GS;

/// <summary>
/// Enable/disable Automatic Status Back (GS a n). n is a bitmask of which status changes to report;
/// 0 disables. While enabled the printer pushes a 4-byte status block on every state change.
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=24
/// </summary>
public class EnableAutoStatusBackCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "a";
    public override bool HasArgs => true;

    private int _n;

    public override void Reset() => _n = 0;

    public override bool InterpretNextChar(char c)
    {
        _n = (byte)c;
        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args) => printer.SetAutoStatusBack(_n);
}
