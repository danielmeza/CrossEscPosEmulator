using System;
using QRCoder;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace ReceiptPrinterEmulator.Emulator.Rendering;

/// <summary>
/// Generates 1D barcodes (via ZXing.Net) and 2D QR codes (via QRCoder) as <see cref="SKBitmap"/>s
/// so they can flow through the same receipt-rendering path as raster images. Only managed matrix
/// APIs are used — the pixels are drawn here with SkiaSharp, so there is no System.Drawing dependency.
/// </summary>
public static class BarcodeRenderer
{
    /// <summary>
    /// Renders a 1D barcode. The symbol is encoded at native (1 dot/module) resolution and then
    /// scaled horizontally by <paramref name="moduleWidth"/> dots for precise bar widths, matching
    /// the ESC/POS GS w / GS h semantics. Optionally appends Human Readable Interpretation text.
    /// </summary>
    public static SKBitmap RenderBarcode1D(
        string content,
        BarcodeFormat format,
        int moduleWidth,
        int heightDots,
        bool showHri,
        string hriFontFamily,
        int hriTextSizeDots)
    {
        moduleWidth = Math.Clamp(moduleWidth, 1, 6);
        heightDots = Math.Max(1, heightDots);

        var writer = new BarcodeWriterGeneric
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = 1,        // force native module resolution; we scale manually
                Height = 1,
                Margin = 0,
                PureBarcode = true
            }
        };

        BitMatrix matrix = writer.Encode(content);
        int modules = matrix.Width;

        int barWidth = modules * moduleWidth;
        int hriHeight = showHri ? hriTextSizeDots + (hriTextSizeDots / 2) : 0;
        int totalHeight = heightDots + hriHeight;

        var bmp = new SKBitmap(barWidth, totalHeight);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);

        using var black = new SKPaint { Color = SKColors.Black, IsAntialias = false };
        for (int x = 0; x < modules; x++)
        {
            if (matrix[x, 0])
                canvas.DrawRect(SKRect.Create(x * moduleWidth, 0, moduleWidth, heightDots), black);
        }

        if (showHri)
            DrawHriText(canvas, content, hriFontFamily, hriTextSizeDots, barWidth, heightDots);

        canvas.Flush();
        return bmp;
    }

    /// <summary>
    /// Renders a QR code. Each QR module is drawn as a <paramref name="moduleSizeDots"/> square,
    /// matching ESC/POS GS ( k function 67. The QRCoder matrix already includes the 4-module quiet zone.
    /// </summary>
    public static SKBitmap RenderQr(string content, int moduleSizeDots, QRCodeGenerator.ECCLevel ecc)
    {
        moduleSizeDots = Math.Clamp(moduleSizeDots, 1, 16);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, ecc);
        var matrix = data.ModuleMatrix;
        int modules = matrix.Count;

        int size = modules * moduleSizeDots;
        var bmp = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);

        using var black = new SKPaint { Color = SKColors.Black, IsAntialias = false };
        for (int row = 0; row < modules; row++)
        {
            var bits = matrix[row];
            for (int col = 0; col < modules; col++)
            {
                if (bits[col])
                    canvas.DrawRect(SKRect.Create(col * moduleSizeDots, row * moduleSizeDots,
                        moduleSizeDots, moduleSizeDots), black);
            }
        }

        canvas.Flush();
        return bmp;
    }

    private static void DrawHriText(SKCanvas canvas, string text, string fontFamily, int sizeDots,
        int barWidth, int yTop)
    {
        var typeface = FontProvider.Get(fontFamily, bold: false, italic: false);
        using var font = new SKFont(typeface, sizeDots);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        float textWidth = font.MeasureText(text);
        float x = (barWidth - textWidth) / 2f;
        float baseline = yTop + (-font.Metrics.Ascent) + (sizeDots / 4f);
        canvas.DrawText(text, x, baseline, font, paint);
    }
}
