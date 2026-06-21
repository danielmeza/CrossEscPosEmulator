using System;
using Ardalis.SmartEnum;

namespace CrossEscPos.Emulator;

/// <summary>
/// DLE EOT n real-time status requests. Each kind carries the status byte it builds, so dispatch is a
/// lookup + polymorphic call instead of a switch on <c>n</c>.
/// </summary>
public sealed class RealtimeStatusRequest : SmartEnum<RealtimeStatusRequest>
{
    public static readonly RealtimeStatusRequest Printer     = new(nameof(Printer), 1, StatusByteBuilder.PrinterStatus);
    public static readonly RealtimeStatusRequest Offline     = new(nameof(Offline), 2, StatusByteBuilder.OfflineStatus);
    public static readonly RealtimeStatusRequest Error       = new(nameof(Error), 3, StatusByteBuilder.ErrorStatus);
    public static readonly RealtimeStatusRequest PaperSensor = new(nameof(PaperSensor), 4, StatusByteBuilder.PaperSensorStatus);

    private readonly Func<PrinterState, byte> _build;

    private RealtimeStatusRequest(string name, int value, Func<PrinterState, byte> build) : base(name, value)
        => _build = build;

    public byte Build(PrinterState state) => _build(state);

    /// <summary>Resolves a DLE EOT parameter, defaulting to the printer status (prior behaviour).</summary>
    public static RealtimeStatusRequest FromParameter(int n) => TryFromValue(n, out var request) ? request : Printer;
}
