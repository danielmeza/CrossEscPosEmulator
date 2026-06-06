using ReceiptPrinterEmulator.Emulator;

namespace ReceiptPrinterEmulator.EscPos.Commands.GS;

/// <summary>
/// Transmit status (GS r n) — n=1 paper sensor, n=2 drawer kick-out connector. Replies one byte
/// to the host. https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=104
/// </summary>
public class TransmitStatusCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "r";
    public override bool HasArgs => true;

    private int _n;

    public override void Reset() => _n = 0;

    public override bool InterpretNextChar(char c)
    {
        _n = (byte)c;
        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args) => printer.TransmitStatus(_n);
}
