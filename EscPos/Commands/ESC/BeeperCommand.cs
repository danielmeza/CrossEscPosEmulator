using ReceiptPrinterEmulator.Emulator;

namespace ReceiptPrinterEmulator.EscPos.Commands.ESC;

/// <summary>
/// Beeper (ESC ( A pL pH ...) — the Epson manufacturer buzzer command. Parameters (number of beeps,
/// pattern, timing) are accepted but the emulator simply triggers its buzzer feedback once.
/// </summary>
public class BeeperCommand : ParenCommand
{
    public override string Prefix => EscPosInterpreter.ESC + "(A";

    public override void Execute(ReceiptPrinter printer, string? args) => printer.Buzz();
}
