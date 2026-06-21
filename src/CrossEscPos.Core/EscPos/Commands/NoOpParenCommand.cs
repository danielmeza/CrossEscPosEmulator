using CrossEscPos.Emulator;
using CrossEscPos.Logging;

namespace CrossEscPos.EscPos.Commands;

/// <summary>
/// A length-prefixed "ESC ( X" / "GS ( X" command that is parsed (so its bytes don't corrupt the
/// stream) but otherwise ignored — used for configuration/mechanism commands that have no visual
/// effect in an emulator (print density, mechanical setup, print control, etc.).
/// </summary>
public sealed class NoOpParenCommand : ParenCommand
{
    private readonly string _prefix;
    private readonly string _name;

    public NoOpParenCommand(string prefix, string name)
    {
        _prefix = prefix;
        _name = name;
    }

    public override string Prefix => _prefix;

    public override void Execute(ReceiptPrinter printer, string? args)
        => Logger.Info($"[{_name}] accepted and ignored ({Params.Count} param bytes)");
}
