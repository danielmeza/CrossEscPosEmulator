using System.Text;
using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Rendering;
using CrossEscPos.Rendering.ImageSharp;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using static CrossEscPos.Core.Tests.EscPosSequence;

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

    // ---- the whole pipeline, asserted on the encoded bytes ---------------------------------------

    /// <summary>
    /// ESC/POS bytes in, a decoded PNG out — the one test that fails if any single ImageSharp drawing
    /// member the backend calls disappears. It drives every <c>IReceiptCanvas</c> primitive, then
    /// asserts the PNG container (IHDR geometry, colour type, IEND) and that the page carries real ink.
    ///
    /// Geometry alone is not enough: a backend that silently drew nothing still produces a valid,
    /// correctly sized, all-white PNG. The ink assertions are what make a dropped draw op fail here.
    /// </summary>
    [Fact]
    public void FullReceipt_EncodesToPng_WithIhdrMatchingTheRender_AndRealInk()
    {
        var printer = NewImageSharpPrinter();

        // Each line below lands on a different canvas primitive:
        printer.FeedEscPos("Plain line" + Bytes(LF));                                  // DrawText
        printer.FeedEscPos(Bytes(GS, '!', 0x11) + "Double" + Bytes(LF, GS, '!', 0x00)); // DrawText under Translate+Scale
        printer.FeedEscPos(Bytes(ESC, '-', 1) + "Underlined" + Bytes(LF, ESC, '-', 0)); // DrawLine
        printer.FeedEscPos(Barcode(4 /*CODE39*/, "ABC123"));                            // DrawRect + DrawImage
        printer.FeedEscPos(Raster());                                                   // DrawImage (bit image)

        using var image = printer.CurrentReceipt.Render();
        var png = new ImageSharpImageEncoder().EncodePng(image);

        // 1. The container header describes exactly the image that was rendered.
        var ihdr = ReadIhdr(png);
        Assert.Equal(PaperConfiguration.Default.GetPaperWidthInPixels(), image.Width);
        Assert.Equal(image.Width, ihdr.Width);
        Assert.Equal(image.Height, ihdr.Height);
        Assert.Equal(8, ihdr.BitDepth);
        Assert.Equal(6, ihdr.ColorType); // 6 = truecolour with alpha, i.e. the Rgba32 backing store

        // 2. A complete stream, not a truncated header: the last chunk is IEND.
        Assert.Equal("IEND", Encoding.ASCII.GetString(png, png.Length - 8, 4));

        // 3. The pixels survive a round trip and the page is neither blank nor solid.
        using var decoded = Image.Load<Rgba32>(png);
        Assert.Equal(image.Width, decoded.Width);
        Assert.Equal(image.Height, decoded.Height);

        long dark = 0;
        decoded.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x].R < 128)
                        dark++;
                }
            }
        });

        long total = (long)image.Width * image.Height;
        Assert.True(dark > 0, "the receipt rendered no dark pixels — the draw operations produced nothing");
        Assert.True(dark < total, "the receipt rendered solid dark — the paper fill colour was lost");
    }

    /// <summary>
    /// Reads the PNG signature and IHDR chunk straight out of the byte array, without an imaging
    /// library, so the assertion is on the encoded bytes rather than on whatever decoded them.
    /// Layout: an 8-byte signature, then IHDR as 4-byte length, 4-byte type, 13 bytes of data, 4-byte CRC.
    /// </summary>
    private static (int Width, int Height, byte BitDepth, byte ColorType) ReadIhdr(byte[] png)
    {
        const int ihdr = 8; // first byte after the signature

        Assert.True(png.Length > ihdr + 21, "stream is too short to hold a PNG signature and IHDR chunk");
        Assert.Equal(PngSignature, png[..PngSignature.Length]);
        Assert.Equal(13u, ReadUInt32BigEndian(png, ihdr));          // IHDR's data length is fixed at 13
        Assert.Equal("IHDR", Encoding.ASCII.GetString(png, ihdr + 4, 4));

        return (
            (int)ReadUInt32BigEndian(png, ihdr + 8),
            (int)ReadUInt32BigEndian(png, ihdr + 12),
            png[ihdr + 16],
            png[ihdr + 17]);
    }

    private static uint ReadUInt32BigEndian(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24)
        | ((uint)bytes[offset + 1] << 16)
        | ((uint)bytes[offset + 2] << 8)
        | bytes[offset + 3];
}
