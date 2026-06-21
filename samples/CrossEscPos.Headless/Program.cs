using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CrossEscPos.Graphics;
using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Rendering;
using CrossEscPos.Rendering.Skia;

namespace CrossEscPos.Headless;

/// <summary>
/// Headless ESC/POS → PNG renderer. Proves the emulator runs with no UI framework: it wires the Core
/// emulator to the SkiaSharp render backend directly — no Avalonia, no window, no UI thread.
///
///   crossescpos-render &lt;input.escpos|--&gt; &lt;output.png&gt;
///
/// Reads ESC/POS bytes from the given file (or stdin when the path is "-"), renders every receipt the
/// stream produced, stacks them top-to-bottom, and writes a single PNG.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: crossescpos-render <input.escpos|-> <output.png>");
            return 2;
        }

        var inputPath = args[0];
        var outputPath = args[1];

        byte[] bytes = inputPath == "-"
            ? ReadAllStdin()
            : File.ReadAllBytes(inputPath);

        // Compose the render backend explicitly — this is the headless composition root.
        var imageFactory = new SkiaImageFactory();
        var typefaces = new SkiaTypefaceProvider();
        var encoder = new SkiaImageEncoder();

        var printer = new ReceiptPrinter(PaperConfiguration.Default, imageFactory, typefaces);

        // The interpreter consumes one char per byte (Latin1), matching the transports.
        printer.FeedEscPos(Encoding.Latin1.GetString(bytes));

        // Render every non-empty receipt the stream produced and stack them.
        var pages = new List<IReceiptImage>();
        foreach (var receipt in printer.ReceiptStack)
        {
            if (receipt.IsEmpty)
                continue;
            pages.Add(receipt.Render());
        }

        if (pages.Count == 0)
        {
            Console.Error.WriteLine("No printable content was produced by the stream.");
            return 1;
        }

        using var combined = ReceiptExporter.StackVertical(pages, imageFactory);
        using (var output = File.Create(outputPath))
            encoder.EncodePng(combined, output);

        foreach (var page in pages)
            page.Dispose();

        Console.WriteLine($"Rendered {pages.Count} receipt(s) -> {outputPath} ({combined.Width}x{combined.Height})");
        return 0;
    }

    private static byte[] ReadAllStdin()
    {
        using var stdin = Console.OpenStandardInput();
        using var buffer = new MemoryStream();
        stdin.CopyTo(buffer);
        return buffer.ToArray();
    }
}
