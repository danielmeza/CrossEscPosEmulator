using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Rendering;
using CrossEscPos.Rendering.Skia;
using QRCoder;
using Xunit;

namespace CrossEscPos.Core.Tests;

/// <summary>
/// End-to-end tests through the real SkiaSharp backend — the same composition the headless sample,
/// desktop and browser hosts use.
/// </summary>
public class SkiaRenderTests
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static ReceiptPrinter NewSkiaPrinter() =>
        new(PaperConfiguration.Default, new SkiaImageFactory(), new SkiaTypefaceProvider());

    [Fact]
    public void Render_ProducesPaperWidthImage()
    {
        var printer = NewSkiaPrinter();
        printer.FeedEscPos("Skia render test\n");

        using var image = printer.CurrentReceipt.Render();

        Assert.Equal(PaperConfiguration.Default.GetPaperWidthInPixels(), image.Width);
        Assert.True(image.Height > 0);
    }

    [Fact]
    public void EncodePng_EmitsValidPngSignature()
    {
        var printer = NewSkiaPrinter();
        printer.FeedEscPos("Encode me\n");

        using var image = printer.CurrentReceipt.Render();
        var png = new SkiaImageEncoder().EncodePng(image);

        Assert.True(png.Length > PngSignature.Length);
        Assert.Equal(PngSignature, png[..PngSignature.Length]);
    }

    [Fact]
    public void BarcodeRenderer_RenderQr_ProducesSquareImage()
    {
        var renderer = new BarcodeRenderer(new SkiaImageFactory(), new SkiaTypefaceProvider());

        using var image = renderer.RenderQr("https://example.com", moduleSizeDots: 3, QRCodeGenerator.ECCLevel.M);

        Assert.True(image.Width > 0);
        Assert.Equal(image.Width, image.Height); // QR symbols are square
    }
}
