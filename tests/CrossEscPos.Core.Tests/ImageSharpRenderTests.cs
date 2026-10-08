using System;
using System.Text;
using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Rendering;
using CrossEscPos.Graphics;
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
    /// ESC/POS bytes in, a decoded PNG out: asserts the container the encoder produced — the IHDR
    /// geometry and colour type read straight from the bytes, and the closing IEND chunk.
    /// </summary>
    [Fact]
    public void FullReceipt_EncodesToPng_WithIhdrMatchingTheRender()
    {
        var png = RenderToPng(FullReceipt, out int width, out int height);

        var ihdr = ReadIhdr(png);
        Assert.Equal(PaperConfiguration.Default.GetPaperWidthInPixels(), width);
        Assert.Equal(width, ihdr.Width);
        Assert.Equal(height, ihdr.Height);
        Assert.Equal(8, ihdr.BitDepth);
        Assert.Equal(6, ihdr.ColorType); // 6 = truecolour with alpha, i.e. the Rgba32 backing store

        // A complete stream, not a truncated header: the last chunk is IEND.
        Assert.Equal("IEND", Encoding.ASCII.GetString(png, png.Length - 8, 4));

        // The pixels survive the round trip at the size the header claims.
        using var decoded = Image.Load<Rgba32>(png);
        Assert.Equal(width, decoded.Width);
        Assert.Equal(height, decoded.Height);
    }

    /// <summary>
    /// Blank paper encodes to a page with no ink at all. This is the baseline the per-primitive tests
    /// below measure against, and it fails if the paper fill is lost.
    /// </summary>
    [Fact]
    public void BlankReceipt_HasNoInk()
    {
        Assert.Equal(0, InkOf(Bytes(LF)));
        Assert.True(WhiteOf(Bytes(LF)) > 0, "blank paper should still be white, not transparent or black");
    }

    /// <summary>
    /// <c>Clear</c> is part of the <c>IReceiptCanvas</c> contract, but the receipt pipeline never calls
    /// it — a page starts from an already-filled image — so no receipt-level test can reach it. It is
    /// covered directly here, because it still has to work for anyone composing a canvas themselves.
    /// </summary>
    [Fact]
    public void Clear_FillsTheWholeSurface()
    {
        const int w = 16, h = 8;
        var factory = new ImageSharpImageFactory();
        using var image = factory.Create(w, h, ReceiptColor.White);
        using (var canvas = factory.CreateCanvas(image))
        {
            canvas.Clear(ReceiptColor.Black);
            canvas.Flush();
        }

        var png = new ImageSharpImageEncoder().EncodePng(image);
        using var decoded = Image.Load<Rgba32>(png);
        Assert.Equal(w * h, CountInk(decoded)); // every pixel, not merely some region
    }

    /// <summary>Text reaches the page — fails if <c>DrawText</c> stops drawing.</summary>
    [Fact]
    public void Text_PutsInkOnThePage()
        => Assert.True(InkOf("Plain line" + Bytes(LF)) > 0, "text drew no ink");

    /// <summary>
    /// Double-width/height text draws more ink than the same string at normal size. This is the only
    /// path that puts a non-identity transform on the canvas, so it fails if the transform is dropped
    /// (the glyphs would come out unscaled) as well as if <c>DrawText</c> stops drawing.
    /// </summary>
    [Fact]
    public void DoubleSizeText_DrawsMoreInkThanNormalSize()
    {
        int normal = InkOf("Double" + Bytes(LF));
        int doubled = InkOf(Bytes(GS, '!', 0x11) + "Double" + Bytes(LF, GS, '!', 0x00));

        Assert.True(normal > 0, "normal-size text drew no ink");
        Assert.True(
            doubled > normal * 2,
            $"double-size text ({doubled} px) should far exceed normal size ({normal} px) — "
            + "the canvas scale transform was not applied");
    }

    /// <summary>
    /// An underline adds ink to the very same text — fails if <c>DrawLine</c> stops drawing, and also
    /// if it draws at 50% grey, which is what ImageSharp.Drawing 3.x does for a boundary-aligned
    /// odd-width stroke and which no 1-bit printer threshold would pick up.
    /// </summary>
    [Fact]
    public void Underline_AddsInkOverTheSameTextPlain()
    {
        int plain = InkOf("Underlined" + Bytes(LF));
        int underlined = InkOf(Bytes(ESC, '-', 1) + "Underlined" + Bytes(LF, ESC, '-', 0));

        Assert.True(plain > 0, "the text itself drew no ink");
        Assert.True(
            underlined > plain,
            $"underlined text ({underlined} px) should carry more ink than plain ({plain} px) — "
            + "the underline was not drawn, or was drawn too light to count as ink");
    }

    /// <summary>
    /// Barcode modules reach the page — fails if <c>DrawRect</c> (the modules) or <c>DrawImage</c>
    /// (placing the barcode bitmap on the receipt) stops drawing.
    /// </summary>
    [Fact]
    public void Barcode_PutsModulesOnThePage()
        => Assert.True(InkOf(Barcode(4 /*CODE39*/, "ABC123")) > 0, "the barcode drew no ink");

    /// <summary>A raster bit image reaches the page — the unscaled <c>DrawImage</c> overload.</summary>
    [Fact]
    public void RasterBitImage_PutsInkOnThePage()
        => Assert.True(InkOf(Raster()) > 0, "the raster bit image drew no ink");

    /// <summary>
    /// A raster wider than the paper is scaled down to the print width, which is the only caller of the
    /// <c>DrawImage(IReceiptImage, ReceiptRect)</c> overload. Fails if that overload stops drawing, and
    /// the width assertion fails if the destination rectangle is not honoured.
    /// </summary>
    [Fact]
    public void OversizeRasterBitImage_IsScaledToPaperWidth_AndDrawsInk()
    {
        int paperWidth = PaperConfiguration.Default.GetPaperWidthInPixels();
        var png = RenderToPng(WideRaster(bytesPerRow: 80, rows: 3), out int width, out int height);

        Assert.Equal(paperWidth, width);
        Assert.True(height > 0);

        using var decoded = Image.Load<Rgba32>(png);
        int ink = CountInk(decoded);
        Assert.True(ink > 0, "the scaled raster drew no ink");
        // 640 px of solid black scaled into 576 px: it must span essentially the full paper width.
        Assert.True(
            ink > paperWidth,
            $"the scaled raster covered only {ink} px, far short of the {paperWidth}-px paper width — "
            + "the destination rectangle was not honoured");
    }

    // ---- helpers ---------------------------------------------------------------------------------

    /// <summary>One receipt that drives text, a scaled run, an underline, a barcode and a raster.</summary>
    private static string FullReceipt =>
        "Plain line" + Bytes(LF)
        + Bytes(GS, '!', 0x11) + "Double" + Bytes(LF, GS, '!', 0x00)
        + Bytes(ESC, '-', 1) + "Underlined" + Bytes(LF, ESC, '-', 0)
        + Barcode(4 /*CODE39*/, "ABC123")
        + Raster();

    /// <summary>GS v 0: a raster bit image <paramref name="bytesPerRow"/> * 8 px wide (all modules on).</summary>
    private static string WideRaster(int bytesPerRow, int rows)
        => Bytes(GS, 'v', '0', 0, bytesPerRow & 0xFF, bytesPerRow >> 8, rows & 0xFF, rows >> 8)
         + new string((char)0xFF, bytesPerRow * rows);

    private static byte[] RenderToPng(string escPos, out int width, out int height)
    {
        var printer = NewImageSharpPrinter();
        printer.FeedEscPos(escPos);

        using var image = printer.CurrentReceipt.Render();
        width = image.Width;
        height = image.Height;
        return new ImageSharpImageEncoder().EncodePng(image);
    }

    /// <summary>Dark pixels on the page after a full encode/decode round trip.</summary>
    private static int InkOf(string escPos)
    {
        var png = RenderToPng(escPos, out _, out _);
        using var decoded = Image.Load<Rgba32>(png);
        return CountInk(decoded);
    }

    private static int WhiteOf(string escPos)
    {
        var png = RenderToPng(escPos, out _, out _);
        using var decoded = Image.Load<Rgba32>(png);
        int white = 0;
        decoded.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x].R == 255 && row[x].A == 255)
                        white++;
                }
            }
        });
        return white;
    }

    /// <summary>
    /// Counts pixels a 1-bit thermal printer would burn. The &lt; 128 threshold is deliberate: it is
    /// what the hardware does, so a primitive that renders at 50% grey counts as having drawn nothing.
    /// </summary>
    private static int CountInk(Image<Rgba32> image)
    {
        int dark = 0;
        image.ProcessPixelRows(accessor =>
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
        return dark;
    }

    /// <summary>
    /// Reads the PNG signature and IHDR chunk straight out of the byte array, without an imaging
    /// library, so the assertion is on the encoded bytes rather than on whatever decoded them.
    /// Layout: an 8-byte signature, then IHDR as 4-byte length, 4-byte type, 13 bytes of data, 4-byte
    /// CRC — 33 bytes in all.
    /// </summary>
    private static (int Width, int Height, byte BitDepth, byte ColorType) ReadIhdr(byte[] png)
    {
        const int ihdr = 8;        // first byte after the signature
        const int chunkEnd = 33;   // 8 + 4 + 4 + 13 + 4

        Assert.True(png.Length >= chunkEnd, "stream is too short to hold a PNG signature and IHDR chunk");
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
