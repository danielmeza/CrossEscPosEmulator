using CrossEscPos.Emulator;

namespace CrossEscPos.EscPos.Commands.GS;

/// <summary>
/// Select cut mode and cut paper
/// https://reference.epson-biz.com/modules/ref_escpos/index.php?content_id=87
/// </summary>
public class SelectCutModeAndCutCommand : BaseCommand
{
    public override string Prefix => EscPosInterpreter.GS + "V";
    public override bool HasArgs => true;
    
    private int _idx;
    private int _m;
    private int _n;

    public override void Reset()
    {
        _idx = 0;
        _m = 0;
        _n = 0;
    }
    
    public override bool InterpretNextChar(char c)
    {
        if (_idx == 0)
        {
            _idx++;
            _m = c;
            _n = 0;

            // A letter-form m (> '1') is cut function B/C/D and carries a second arg (n).
            return _m > '1';
        }
       
        if (_idx == 1)
        {
            _idx++;
            _n = c;
        }
        
        return false;
    }

    public override void Execute(ReceiptPrinter printer, string? args)
    {
        var mode = CutMode.FromParameter(_n);
        printer.Cut(mode.Function, mode.Shape, _n);
    }
}