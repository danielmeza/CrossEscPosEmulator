namespace CrossEscPos.Graphics;

/// <summary>
/// A backend-agnostic 32-bit RGBA color. Receipts are monochrome, but a full color keeps the surface
/// general (and lets the raster/bit-image commands carry grayscale values straight through).
/// </summary>
public readonly record struct ReceiptColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly ReceiptColor Black = new(0, 0, 0);
    public static readonly ReceiptColor White = new(255, 255, 255);

    /// <summary>An opaque gray where R=G=B=<paramref name="value"/>.</summary>
    public static ReceiptColor Gray(byte value) => new(value, value, value);
}
