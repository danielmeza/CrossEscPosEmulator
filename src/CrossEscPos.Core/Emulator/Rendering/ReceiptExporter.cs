using System;
using System.Collections.Generic;
using CrossEscPos.Graphics;

namespace CrossEscPos.Emulator.Rendering;

public static class ReceiptExporter
{
    /// <summary>
    /// Composes several receipt images into a single tall image, stacked top-to-bottom on a white
    /// background with a gap between each. Used by "export all". The caller owns the source images; the
    /// returned image is new.
    /// </summary>
    public static IReceiptImage StackVertical(IReadOnlyList<IReceiptImage> receipts,
        IReceiptImageFactory imageFactory, int gap = 24)
    {
        if (receipts.Count == 0)
            return imageFactory.Create(1, 1, ReceiptColor.White);

        int width = 0;
        int height = 0;
        foreach (var r in receipts)
        {
            width = Math.Max(width, r.Width);
            height += r.Height;
        }
        height += gap * (receipts.Count - 1);

        var combined = imageFactory.Create(width, Math.Max(1, height), ReceiptColor.White);
        using var canvas = imageFactory.CreateCanvas(combined);

        int y = 0;
        foreach (var r in receipts)
        {
            // Center each receipt horizontally within the widest one.
            int x = (width - r.Width) / 2;
            canvas.DrawImage(r, x, y);
            y += r.Height + gap;
        }

        canvas.Flush();
        return combined;
    }
}
