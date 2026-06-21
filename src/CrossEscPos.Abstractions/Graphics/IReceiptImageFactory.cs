namespace CrossEscPos.Graphics;

/// <summary>
/// Creates images and the canvases that draw onto them. This is the backend entry point the emulator
/// core depends on; the desktop/browser hosts (and the headless sample) supply a concrete factory
/// (e.g. the SkiaSharp one).
/// </summary>
public interface IReceiptImageFactory
{
    /// <summary>Creates a blank image of the given size filled with <paramref name="fill"/>.</summary>
    IReceiptImage Create(int width, int height, ReceiptColor fill);

    /// <summary>
    /// Creates an image from row-major pixels (length must be <paramref name="width"/> *
    /// <paramref name="height"/>). Used by the raster / bit-image ESC/POS commands.
    /// </summary>
    IReceiptImage FromPixels(int width, int height, ReceiptColor[] rowMajorPixels);

    /// <summary>Creates a canvas that draws onto <paramref name="image"/>.</summary>
    IReceiptCanvas CreateCanvas(IReceiptImage image);
}
