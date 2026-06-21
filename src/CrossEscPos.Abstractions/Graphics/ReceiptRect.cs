namespace CrossEscPos.Graphics;

/// <summary>An axis-aligned rectangle in device pixels (top-left origin), mirroring SKRect.Create semantics.</summary>
public readonly record struct ReceiptRect(float X, float Y, float Width, float Height)
{
    public static ReceiptRect Create(float x, float y, float width, float height) => new(x, y, width, height);
}
