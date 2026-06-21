using System;

namespace CrossEscPos.Graphics;

/// <summary>
/// A rasterized image (a receipt page, a barcode, an uploaded bit image). Produced by an
/// <see cref="IReceiptImageFactory"/> and consumed by <see cref="IReceiptCanvas"/> and
/// <see cref="IImageEncoder"/>. The owner disposes it.
/// </summary>
public interface IReceiptImage : IDisposable
{
    int Width { get; }
    int Height { get; }

    /// <summary>Returns an independent copy whose lifetime the caller owns.</summary>
    IReceiptImage Copy();
}
