using System;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Rendering;
using CrossEscPos.Rendering.Skia;

namespace CrossEscPos.Wasm;

/// <summary>
/// JavaScript-callable surface for rendering ESC/POS to PNG entirely in the browser — no .NET on the
/// caller's side. Exposed via <c>[JSExport]</c>; reached from JS through <c>getAssemblyExports()</c>.
/// </summary>
[SupportedOSPlatform("browser")]
public partial class ReceiptInterop
{
    private static readonly SkiaImageFactory ImageFactory = new();
    private static readonly SkiaTypefaceProvider Typefaces = new();
    private static readonly SkiaImageEncoder Encoder = new();

    /// <summary>
    /// Renders an ESC/POS byte stream to a single PNG (every non-empty receipt stacked top-to-bottom).
    /// Returns an empty array when the stream produces no printable content.
    /// </summary>
    [JSExport]
    internal static byte[] RenderToPng(byte[] escpos)
    {
        var printer = new ReceiptPrinter(PaperConfiguration.Default, ImageFactory, Typefaces);
        printer.FeedEscPos(escpos);

        var pages = printer.ReceiptStack.Where(r => !r.IsEmpty).Select(r => r.Render()).ToList();
        if (pages.Count == 0)
            return Array.Empty<byte>();

        try
        {
            using var combined = ReceiptExporter.StackVertical(pages, ImageFactory);
            return Encoder.EncodePng(combined);
        }
        finally
        {
            foreach (var page in pages)
                page.Dispose();
        }
    }

    /// <summary>Number of receipts (cuts) the stream would produce.</summary>
    [JSExport]
    internal static int CountReceipts(byte[] escpos)
    {
        var printer = new ReceiptPrinter(PaperConfiguration.Default, ImageFactory, Typefaces);
        printer.FeedEscPos(escpos);
        return printer.ReceiptStack.Count(r => !r.IsEmpty);
    }
}
