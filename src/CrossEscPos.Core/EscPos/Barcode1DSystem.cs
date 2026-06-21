using System.Linq;
using Ardalis.SmartEnum;
using ZXing;

namespace CrossEscPos.EscPos;

/// <summary>
/// GS k 1D barcode systems. ESC/POS encodes each symbology twice — Function A (m = 0..6) and Function
/// B (m = 65..73). The SmartEnum value is the Function-B code; the Function-A code (where one exists)
/// and the ZXing format are carried alongside, so decoding <c>m</c> is a lookup, not a switch.
/// </summary>
public sealed class Barcode1DSystem : SmartEnum<Barcode1DSystem>
{
    public static readonly Barcode1DSystem UpcA    = new(nameof(UpcA), 65, 0, BarcodeFormat.UPC_A);
    public static readonly Barcode1DSystem UpcE    = new(nameof(UpcE), 66, 1, BarcodeFormat.UPC_E);
    public static readonly Barcode1DSystem Ean13   = new(nameof(Ean13), 67, 2, BarcodeFormat.EAN_13);
    public static readonly Barcode1DSystem Ean8    = new(nameof(Ean8), 68, 3, BarcodeFormat.EAN_8);
    public static readonly Barcode1DSystem Code39  = new(nameof(Code39), 69, 4, BarcodeFormat.CODE_39);
    public static readonly Barcode1DSystem Itf     = new(nameof(Itf), 70, 5, BarcodeFormat.ITF);
    public static readonly Barcode1DSystem Codabar = new(nameof(Codabar), 71, 6, BarcodeFormat.CODABAR);
    public static readonly Barcode1DSystem Code93  = new(nameof(Code93), 72, null, BarcodeFormat.CODE_93);
    public static readonly Barcode1DSystem Code128 = new(nameof(Code128), 73, null, BarcodeFormat.CODE_128);

    /// <summary>The Function-A code (0..6), or null for systems only available via Function B.</summary>
    public int? FunctionACode { get; }

    /// <summary>The ZXing format used to encode this symbology.</summary>
    public BarcodeFormat Format { get; }

    private Barcode1DSystem(string name, int functionBCode, int? functionACode, BarcodeFormat format)
        : base(name, functionBCode)
    {
        FunctionACode = functionACode;
        Format = format;
    }

    /// <summary>Resolves a GS k system code <c>m</c> from either Function A or B; null if unsupported.</summary>
    public static Barcode1DSystem? FromCommandCode(int m)
        => TryFromValue(m, out var byFunctionB) ? byFunctionB : List.FirstOrDefault(s => s.FunctionACode == m);
}
