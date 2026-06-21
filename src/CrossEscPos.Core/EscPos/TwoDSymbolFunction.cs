using System;
using Ardalis.SmartEnum;
using CrossEscPos.Emulator;
using CrossEscPos.Logging;

namespace CrossEscPos.EscPos;

/// <summary>
/// GS ( k 2D-symbol functions (the <c>fn</c> parameter). Each function carries the action it performs
/// on the printer, so dispatching a 2D command is a lookup + call rather than a switch on <c>fn</c>.
/// The action receives the printer, the symbol family (<c>cn</c>) and the raw parameter bytes.
/// </summary>
public sealed class TwoDSymbolFunction : SmartEnum<TwoDSymbolFunction>
{
    public static readonly TwoDSymbolFunction SelectModel = new(nameof(SelectModel), 65,
        (_, _, p) => Logger.Info($"2D select model/options: {(p.Length > 0 ? (int)p[0] : 0)}"));

    public static readonly TwoDSymbolFunction SetModuleSize = new(nameof(SetModuleSize), 67,
        (printer, _, p) => { if (p.Length > 0) printer.SetQrModuleSize((byte)p[0]); });

    public static readonly TwoDSymbolFunction SetErrorCorrection = new(nameof(SetErrorCorrection), 69,
        (printer, _, p) => { if (p.Length > 0) printer.SetQrErrorCorrection(QrErrorCorrectionLevel.FromParameter((byte)p[0])); });

    public static readonly TwoDSymbolFunction StoreData = new(nameof(StoreData), 80,
        (printer, cn, p) => printer.Store2DData(cn, p.Length > 1 ? p.Substring(1) : string.Empty));

    public static readonly TwoDSymbolFunction PrintSymbol = new(nameof(PrintSymbol), 81,
        (printer, _, _) => printer.Print2D());

    public static readonly TwoDSymbolFunction TransmitSize = new(nameof(TransmitSize), 82,
        (_, _, _) => { /* size info — not applicable to an emulator */ });

    private readonly Action<ReceiptPrinter, int, string> _apply;

    private TwoDSymbolFunction(string name, int value, Action<ReceiptPrinter, int, string> apply) : base(name, value)
        => _apply = apply;

    /// <summary>Performs this function against <paramref name="printer"/> for symbol family <paramref name="cn"/>.</summary>
    public void Apply(ReceiptPrinter printer, int cn, string parameters) => _apply(printer, cn, parameters);
}
