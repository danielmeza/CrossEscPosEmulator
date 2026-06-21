using Ardalis.SmartEnum;
using CrossEscPos.Graphics;
using QRCoder;
using ZXing;

namespace CrossEscPos.Emulator.Rendering;

/// <summary>
/// ESC/POS 2D symbol families (the <c>cn</c> parameter of GS ( k). Each family knows how to render
/// itself, so printing a stored 2D symbol is a polymorphic call rather than a switch on <c>cn</c>.
/// </summary>
public abstract class TwoDimensionCode : SmartEnum<TwoDimensionCode>
{
    public static readonly TwoDimensionCode Pdf417     = new ZXingSymbol(48, nameof(Pdf417), BarcodeFormat.PDF_417);
    public static readonly TwoDimensionCode QrCode     = new QrSymbol(49, nameof(QrCode));
    public static readonly TwoDimensionCode DataMatrix = new ZXingSymbol(54, nameof(DataMatrix), BarcodeFormat.DATA_MATRIX);
    public static readonly TwoDimensionCode Aztec      = new ZXingSymbol(55, nameof(Aztec), BarcodeFormat.AZTEC);

    private TwoDimensionCode(string name, int value) : base(name, value) { }

    /// <summary>Renders the stored data as this 2D family to an image.</summary>
    public abstract IReceiptImage Render(BarcodeRenderer renderer, string data, int moduleSize,
        QRCodeGenerator.ECCLevel ecc);

    /// <summary>Resolves a <c>cn</c> value, defaulting to QR for unknown families (prior behaviour).</summary>
    public static TwoDimensionCode FromCn(int cn) => TryFromValue(cn, out var code) ? code : QrCode;

    private sealed class QrSymbol : TwoDimensionCode
    {
        public QrSymbol(int value, string name) : base(name, value) { }

        public override IReceiptImage Render(BarcodeRenderer renderer, string data, int moduleSize,
            QRCodeGenerator.ECCLevel ecc)
            => renderer.RenderQr(data, moduleSize, ecc);
    }

    private sealed class ZXingSymbol : TwoDimensionCode
    {
        private readonly BarcodeFormat _format;

        public ZXingSymbol(int value, string name, BarcodeFormat format) : base(name, value) => _format = format;

        public override IReceiptImage Render(BarcodeRenderer renderer, string data, int moduleSize,
            QRCodeGenerator.ECCLevel ecc)
            => renderer.Render2D(data, _format, moduleSize);
    }
}
