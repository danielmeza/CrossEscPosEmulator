using System;
using CrossEscPos.Graphics;
using QRCoder;
using ZXing;
using ZXing.Common;

namespace CrossEscPos.Emulator.Rendering;

/// <summary>
/// Generates 1D barcodes (via ZXing.Net) and 2D QR codes (via QRCoder) as <see cref="IReceiptImage"/>s
/// so they can flow through the same receipt-rendering path as raster images. Only managed matrix APIs
/// are used; the modules are drawn through the backend-agnostic <see cref="IReceiptCanvas"/>, so there
/// is no graphics-backend dependency here.
/// </summary>
public class BarcodeRenderer
{
    private readonly IReceiptImageFactory _imageFactory;
    private readonly ITypefaceProvider _typefaces;

    public BarcodeRenderer(IReceiptImageFactory imageFactory, ITypefaceProvider typefaces)
    {
        _imageFactory = imageFactory;
        _typefaces = typefaces;
    }

    /// <summary>
    /// Renders a 1D barcode. The symbol is encoded at native (1 dot/module) resolution and then
    /// scaled horizontally by <paramref name="moduleWidth"/> dots for precise bar widths, matching
    /// the ESC/POS GS w / GS h semantics. Optionally appends Human Readable Interpretation text.
    /// </summary>
    public IReceiptImage RenderBarcode1D(
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

        var image = _imageFactory.Create(barWidth, totalHeight, ReceiptColor.White);
        using var canvas = _imageFactory.CreateCanvas(image);

        for (int x = 0; x < modules; x++)
        {
            if (matrix[x, 0])
                canvas.DrawRect(ReceiptRect.Create(x * moduleWidth, 0, moduleWidth, heightDots), ReceiptColor.Black);
        }

        if (showHri)
            DrawHriText(canvas, content, hriFontFamily, hriTextSizeDots, barWidth, heightDots);

        canvas.Flush();
        return image;
    }

    /// <summary>
    /// Renders a QR code. Each QR module is drawn as a <paramref name="moduleSizeDots"/> square,
    /// matching ESC/POS GS ( k function 67. The QRCoder matrix already includes the 4-module quiet zone.
    /// </summary>
    public IReceiptImage RenderQr(string content, int moduleSizeDots, QRCodeGenerator.ECCLevel ecc)
    {
        moduleSizeDots = Math.Clamp(moduleSizeDots, 1, 16);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, ecc);
        var matrix = data.ModuleMatrix;
        int modules = matrix.Count;

        int size = modules * moduleSizeDots;
        var image = _imageFactory.Create(size, size, ReceiptColor.White);
        using var canvas = _imageFactory.CreateCanvas(image);

        for (int row = 0; row < modules; row++)
        {
            var bits = matrix[row];
            for (int col = 0; col < modules; col++)
            {
                if (bits[col])
                    canvas.DrawRect(ReceiptRect.Create(col * moduleSizeDots, row * moduleSizeDots,
                        moduleSizeDots, moduleSizeDots), ReceiptColor.Black);
            }
        }

        canvas.Flush();
        return image;
    }

    /// <summary>
    /// Renders a 2D matrix symbology (PDF417, DataMatrix, Aztec, …) via ZXing. The symbol is encoded
    /// at native resolution and each module drawn as a <paramref name="moduleSize"/> square.
    /// </summary>
    public IReceiptImage Render2D(string content, BarcodeFormat format, int moduleSize)
    {
        moduleSize = Math.Clamp(moduleSize, 1, 16);

        var writer = new BarcodeWriterGeneric
        {
            Format = format,
            Options = new EncodingOptions { Width = 0, Height = 0, Margin = 0, PureBarcode = true }
        };

        BitMatrix matrix = writer.Encode(content);
        int cols = matrix.Width, rows = matrix.Height;

        var image = _imageFactory.Create(cols * moduleSize, rows * moduleSize, ReceiptColor.White);
        using var canvas = _imageFactory.CreateCanvas(image);

        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                if (matrix[x, y])
                    canvas.DrawRect(ReceiptRect.Create(x * moduleSize, y * moduleSize, moduleSize, moduleSize), ReceiptColor.Black);

        canvas.Flush();
        return image;
    }

    private void DrawHriText(IReceiptCanvas canvas, string text, string fontFamily, int sizeDots,
        int barWidth, int yTop)
    {
        using var font = _typefaces.GetFont(fontFamily, bold: false, italic: false, sizeDots);

        float textWidth = font.MeasureText(text);
        float x = (barWidth - textWidth) / 2f;
        float baseline = yTop + (-font.Metrics.Ascent) + (sizeDots / 4f);
        canvas.DrawText(text, x, baseline, font, ReceiptColor.Black);
    }
}
