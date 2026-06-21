using System.Collections.Generic;
using System.Text;
using Ardalis.SmartEnum;

namespace CrossEscPos.Emulator;

/// <summary>
/// GS I printer-information requests. Each request carries the bytes it replies with — the single-byte
/// "Info A" IDs (model/type/ROM) and the framed "Info B" text responses (<c>0x5F &lt;text&gt; 0x00</c>) —
/// so answering is a lookup instead of a switch on <c>n</c>. The parameter accepts numeric or ASCII form.
/// </summary>
public sealed class PrinterIdRequest : SmartEnum<PrinterIdRequest>
{
    // Info A — single-byte IDs.
    public static readonly PrinterIdRequest ModelId    = new(nameof(ModelId), 1, new byte[] { 0x02 });
    public static readonly PrinterIdRequest TypeId      = new(nameof(TypeId), 2, new byte[] { 0x00 });
    public static readonly PrinterIdRequest RomVersion  = new(nameof(RomVersion), 3, new byte[] { 0x01 });

    // Info B — text responses framed as 0x5F <ascii> 0x00.
    public static readonly PrinterIdRequest FirmwareVersion = new(nameof(FirmwareVersion), 65, InfoB("1.0"));
    public static readonly PrinterIdRequest MakerName       = new(nameof(MakerName), 66, InfoB("CrossEscPos"));
    public static readonly PrinterIdRequest ModelName       = new(nameof(ModelName), 67, InfoB("EMU-80"));

    // Fallback for unknown ids — a generic Info B name (value -1 never matches a real parameter).
    public static readonly PrinterIdRequest Unknown = new(nameof(Unknown), -1, InfoB("EMU"));

    public byte[] Response { get; }

    private PrinterIdRequest(string name, int value, byte[] response) : base(name, value) => Response = response;

    /// <summary>Resolves a GS I parameter (numeric or ASCII digit), defaulting to a generic Info B reply.</summary>
    public static PrinterIdRequest FromParameter(int n)
        => TryFromValue(n is >= '0' and <= '9' ? n - '0' : n, out var request) ? request : Unknown;

    private static byte[] InfoB(string text)
    {
        var bytes = new List<byte> { 0x5F };
        bytes.AddRange(Encoding.ASCII.GetBytes(text));
        bytes.Add(0x00);
        return bytes.ToArray();
    }
}
