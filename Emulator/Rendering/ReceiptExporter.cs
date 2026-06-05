using System;
using System.Collections.Generic;
using SkiaSharp;

namespace ReceiptPrinterEmulator.Emulator.Rendering;

public static class ReceiptExporter
{
    /// <summary>
    /// Composes several receipt bitmaps into a single tall image, stacked top-to-bottom on a white
    /// background with a gap between each. Used by "export all". The caller owns the source bitmaps;
    /// the returned bitmap is a new copy.
    /// </summary>
    public static SKBitmap StackVertical(IReadOnlyList<SKBitmap> receipts, int gap = 24)
    {
        if (receipts.Count == 0)
            return new SKBitmap(1, 1);

        int width = 0;
        int height = 0;
        foreach (var r in receipts)
        {
            width = Math.Max(width, r.Width);
            height += r.Height;
        }
        height += gap * (receipts.Count - 1);

        var combined = new SKBitmap(width, Math.Max(1, height));
        using var canvas = new SKCanvas(combined);
        canvas.Clear(SKColors.White);

        int y = 0;
        foreach (var r in receipts)
        {
            // Center each receipt horizontally within the widest one.
            int x = (width - r.Width) / 2;
            canvas.DrawBitmap(r, x, y);
            y += r.Height + gap;
        }

        canvas.Flush();
        return combined;
    }
}
