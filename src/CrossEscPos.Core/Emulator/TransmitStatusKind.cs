using System;
using Ardalis.SmartEnum;

namespace CrossEscPos.Emulator;

/// <summary>
/// GS r n transmit-status requests. Each kind carries the status byte it builds. The parameter accepts
/// both numeric (1, 2) and ASCII ('1', '2') forms, as ESC/POS allows.
/// </summary>
public sealed class TransmitStatusKind : SmartEnum<TransmitStatusKind>
{
    public static readonly TransmitStatusKind Paper  = new(nameof(Paper), 1, StatusByteBuilder.TransmitPaperStatus);
    public static readonly TransmitStatusKind Drawer = new(nameof(Drawer), 2, StatusByteBuilder.TransmitDrawerStatus);

    private readonly Func<PrinterState, byte> _build;

    private TransmitStatusKind(string name, int value, Func<PrinterState, byte> build) : base(name, value)
        => _build = build;

    public byte Build(PrinterState state) => _build(state);

    /// <summary>Resolves a GS r parameter (numeric or ASCII digit); null when unsupported.</summary>
    public static TransmitStatusKind? FromParameter(int n)
        => TryFromValue(n >= '0' ? n - '0' : n, out var kind) ? kind : null;
}
