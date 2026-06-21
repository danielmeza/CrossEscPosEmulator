using CrossEscPos.Emulator;

namespace CrossEscPos.EscPos.Commands.GS;

/// <summary>
/// Select character size
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=34
/// </summary>
public class SelectCharacterSizeCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "!";
    public override bool HasArgs => true;
    
    private byte _n;

    public override void Reset()
    {
        _n = 0;
    }
    
    public override bool InterpretNextChar(char c)
    {
        _n = (byte)c;
        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args)
    {
        // GS ! n: bits 6,5,4 = width magnification - 1; bits 2,1,0 = height magnification - 1 (1..8).
        var widthScale = ((_n >> 4) & 0b111) + 1;
        var heightScale = (_n & 0b111) + 1;

        printer.SelectCharacterSize(widthScale, heightScale);
    }
}