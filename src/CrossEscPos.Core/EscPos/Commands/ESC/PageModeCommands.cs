using CrossEscPos.Emulator;

namespace CrossEscPos.EscPos.Commands.ESC;

/// <summary>ESC L — select page mode.</summary>
public class SelectPageModeCommand : BaseCommandNoArgs
{
    public override string Prefix => EscPosInterpreter.ESC + "L";
    public override void Execute(ReceiptPrinter printer, string? args) => printer.EnterPageMode();
}

/// <summary>ESC S — select standard mode.</summary>
public class SelectStandardModeCommand : BaseCommandNoArgs
{
    public override string Prefix => EscPosInterpreter.ESC + "S";
    public override void Execute(ReceiptPrinter printer, string? args) => printer.SelectStandardMode();
}
