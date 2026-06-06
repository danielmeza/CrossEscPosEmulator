using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Logging;

namespace ReceiptPrinterEmulator.EscPos.Commands;

/// <summary>
/// A command with a fixed number of argument bytes that are consumed (so they don't corrupt the
/// stream) and logged but otherwise ignored. Used for positioning/area commands the emulator
/// approximates (page-mode print area/direction, absolute/relative position).
/// </summary>
public sealed class FixedArgNoOpCommand : BaseCommand
{
    private readonly string _prefix;
    private readonly int _count;
    private readonly string _name;
    private int _read;

    public FixedArgNoOpCommand(string prefix, int count, string name)
    {
        _prefix = prefix;
        _count = count;
        _name = name;
    }

    public override string Prefix => _prefix;
    public override bool HasArgs => _count > 0;

    public override void Reset() => _read = 0;

    public override bool InterpretNextChar(char c)
    {
        _read++;
        return _read < _count;
    }

    public override void Execute(ReceiptPrinter printer, string? args)
        => Logger.Info($"[{_name}] accepted and ignored");
}
