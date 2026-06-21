using CrossEscPos.EscPos;
using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Enums;
using CrossEscPos.Emulator.Rendering;
using QRCoder;
using Xunit;

namespace CrossEscPos.Core.Tests;

/// <summary>
/// Locks the SmartEnum code maps that replaced the magic-number switches (ESC/POS code tables, 1D/2D
/// systems, status requests) — value lookups and unknown-value fallbacks.
/// </summary>
public class SmartEnumTests
{
    [Theory]
    [InlineData(0, 437)]
    [InlineData(2, 850)]
    [InlineData(16, 1252)]
    [InlineData(19, 858)]
    public void CharacterCodeTable_MapsTableToCodePage(int table, int expectedCodePage)
        => Assert.Equal(expectedCodePage, CharacterCodeTable.FromTableId(table).CodePage);

    [Fact]
    public void CharacterCodeTable_UnknownTable_FallsBackToPc437()
        => Assert.Equal(CharacterCodeTable.Pc437, CharacterCodeTable.FromTableId(99));

    [Theory]
    [InlineData(48, "Pdf417")]
    [InlineData(49, "QrCode")]
    [InlineData(54, "DataMatrix")]
    [InlineData(55, "Aztec")]
    public void TwoDimensionCode_ResolvesCn(int cn, string expectedName)
        => Assert.Equal(expectedName, TwoDimensionCode.FromCn(cn).Name);

    [Fact]
    public void TwoDimensionCode_UnknownCn_FallsBackToQr()
        => Assert.Equal(TwoDimensionCode.QrCode, TwoDimensionCode.FromCn(99));

    [Fact]
    public void TwoDimensionCode_QrCode_RendersSquareImage()
    {
        var renderer = new BarcodeRenderer(new FakeImageFactory(), new FakeTypefaceProvider());

        using var image = TwoDimensionCode.QrCode.Render(renderer, "hello", 3, QRCodeGenerator.ECCLevel.M);

        Assert.True(image.Width > 0);
        Assert.Equal(image.Width, image.Height); // QR symbols are square
    }

    [Theory]
    [InlineData(0, "UpcA")]    // Function A
    [InlineData(65, "UpcA")]   // Function B (same symbology)
    [InlineData(6, "Codabar")]
    [InlineData(72, "Code93")] // Function-B only
    [InlineData(73, "Code128")]
    public void Barcode1DSystem_ResolvesFunctionAandB(int m, string expectedName)
        => Assert.Equal(expectedName, Barcode1DSystem.FromCommandCode(m)!.Name);

    [Fact]
    public void Barcode1DSystem_UnknownCode_IsNull()
        => Assert.Null(Barcode1DSystem.FromCommandCode(200));

    [Theory]
    [InlineData(1, "Printer")]
    [InlineData(2, "Offline")]
    [InlineData(4, "PaperSensor")]
    [InlineData(99, "Printer")] // unknown -> default
    public void RealtimeStatusRequest_ResolvesParameter(int n, string expectedName)
        => Assert.Equal(expectedName, RealtimeStatusRequest.FromParameter(n).Name);

    [Theory]
    [InlineData(1, "Paper")]   // numeric
    [InlineData(49, "Paper")]  // ASCII '1'
    [InlineData(2, "Drawer")]
    [InlineData(50, "Drawer")] // ASCII '2'
    public void TransmitStatusKind_AcceptsNumericAndAscii(int n, string expectedName)
        => Assert.Equal(expectedName, TransmitStatusKind.FromParameter(n)!.Name);

    [Fact]
    public void TransmitStatusKind_UnknownParameter_IsNull()
        => Assert.Null(TransmitStatusKind.FromParameter(9));

    [Theory]
    [InlineData(0, TextJustification.Left)]
    [InlineData(48, TextJustification.Left)]   // ASCII '0'
    [InlineData(1, TextJustification.Center)]
    [InlineData(50, TextJustification.Right)]  // ASCII '2'
    public void JustificationMode_AcceptsNumericAndAscii(int n, TextJustification expected)
        => Assert.Equal(expected, JustificationMode.FromParameter(n)!.Justification);

    [Fact]
    public void JustificationMode_UnknownParameter_IsNull()
        => Assert.Null(JustificationMode.FromParameter(9));

    [Theory]
    [InlineData(0, PrinterFont.FontA)]
    [InlineData(48, PrinterFont.FontA)]  // ASCII '0'
    [InlineData(52, PrinterFont.FontE)]  // ASCII '4'
    [InlineData('a', PrinterFont.SpecialFontA)]
    [InlineData('b', PrinterFont.SpecialFontB)]
    public void FontSelection_ResolvesFontCodes(int n, PrinterFont expected)
        => Assert.Equal(expected, FontSelection.FromParameter(n)!.Font);

    [Theory]
    [InlineData(0, CutFunction.Cut, CutShape.Full)]
    [InlineData(49, CutFunction.Cut, CutShape.Partial)]   // ASCII '1'
    [InlineData('A', CutFunction.FeedAndCut, CutShape.Full)]
    [InlineData('h', CutFunction.FeedAndCutAndReverse, CutShape.Partial)]
    [InlineData(200, CutFunction.Cut, CutShape.Full)]     // unknown -> full cut
    public void CutMode_ResolvesFunctionAndShape(int n, CutFunction function, CutShape shape)
    {
        var mode = CutMode.FromParameter(n);
        Assert.Equal(function, mode.Function);
        Assert.Equal(shape, mode.Shape);
    }

    [Theory]
    [InlineData('0', QRCodeGenerator.ECCLevel.L)]
    [InlineData('1', QRCodeGenerator.ECCLevel.M)]
    [InlineData('3', QRCodeGenerator.ECCLevel.H)]
    [InlineData(99, QRCodeGenerator.ECCLevel.M)] // unknown -> medium
    public void QrErrorCorrectionLevel_MapsParameter(int n, QRCodeGenerator.ECCLevel expected)
        => Assert.Equal(expected, QrErrorCorrectionLevel.FromParameter(n));
}
