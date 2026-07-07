using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Rendering;
using CrossEscPos.Rendering.ImageSharp;
using QRCoder;
using Xunit;

namespace CrossEscPos.Core.Tests;

/// <summary>
/// End-to-end render tests for the managed <see cref="ImageSharpImageFactory"/> backend — the mirror of
/// <see cref="SkiaRenderTests"/>. This backend has no native dependency, so it works in Blazor WASM.
/// </summary>
public class ImageSharpRenderTests
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static ReceiptPrinter NewImageSharpPrinter() =>
        new(PaperConfiguration.Default, new ImageSharpImageFactory(), new ImageSharpTypefaceProvider());

    [Fact]
    public void Render_ProducesPaperWidthImage()
    {
        var printer = NewImageSharpPrinter();
        printer.FeedEscPos("ImageSharp render test\n");

        using var image = printer.CurrentReceipt.Render();

        Assert.Equal(PaperConfiguration.Default.GetPaperWidthInPixels(), image.Width);
        Assert.True(image.Height > 0);
    }

    [Fact]
    public void EncodePng_EmitsValidPngSignature()
    {
        var printer = NewImageSharpPrinter();
        printer.FeedEscPos("Encode me\n");

        using var image = printer.CurrentReceipt.Render();
        var png = new ImageSharpImageEncoder().EncodePng(image);

        Assert.True(png.Length > PngSignature.Length);
        Assert.Equal(PngSignature, png[..PngSignature.Length]);
    }

    [Fact]
    public void BarcodeRenderer_RenderQr_ProducesSquareImage()
    {
        var renderer = new BarcodeRenderer(new ImageSharpImageFactory(), new ImageSharpTypefaceProvider());

        using var image = renderer.RenderQr("https://example.com", moduleSizeDots: 3, QRCodeGenerator.ECCLevel.M);

        Assert.True(image.Width > 0);
        Assert.Equal(image.Width, image.Height); // QR symbols are square
    }
}
